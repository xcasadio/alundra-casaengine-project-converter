# Critique de complétude, thème 2 (attaques du joueur et usages d'objets)

Étiquettes : [F] fait lu, [M] mesuré (binaire France ou export), [I] inférence.
Je n'ai lancé aucun build ni test, et je n'ai rien écrit dans le dépôt. J'ai refait les lectures avec ma propre bibliothèque MIPS (`lib.py`, `c1.py` à `c14.py`, `hdr.py`), sans reprendre les scripts du lecteur. Ils sont sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic/`.

## A. CONFIRMÉ (re-dérivé indépendamment)
1. [M] Tables de départ, d'annulation et de pilote.
   - Elles ont 20 entrées (0x1F à 0x32) à `0x8002339C`, `0x800233EC` et `0x800235F4`.
   - Les refus au Rond sont aux entrées 0x21, 0x22, 0x28 et 0x2A, donc #33, #34, #40 et #42. Hors plage : son 3.
   - Les huit départs distincts sont ceux du lecteur. Le répartiteur `0x8003499C` en est le seul appelant, avec le Breuvage (`0x80032818`). Aucun objet ne s'utilise depuis un menu.
2. [M] `PlayerTryAttack` n'est appelé que depuis `0x80031E84` (états 02, 2B, 2C, 2D) et `0x80031F54` (état 29). Les états 09 et 0A sont les lancers d'objet porté (écrits en `0x800323D4` et `0x800323EC`). Ils relèvent d'ATK-18, pas des armes.
3. [M] Les 12 enregistrements d'armes, la table objet vers enregistrement, les drapeaux d'animation, les 19 lignes d'attaque du héros et les sprites globaux 0 à 30 (handlers, attributs, puissances) sont identiques au rapport.
4. [M] `DecreaseMp` (`0x8004DF3C`) n'a qu'un appelant, le départ des tomes (`0x80034948`). Les bâtons et les arcs ne coûtent donc aucun PM.
5. [M] Les quatre consommateurs des bottes (`0x80031AD0`, `0x800368BC`, `0x800369E0`, `0x800374FC`), le gant (`0x8002EDF8`) et les deux usages du Breuvage sont exacts. Le geste Start+Select dure 120 images, consomme le Breuvage s'il y en a un, puis met les PV à 0.
6. [M] Les catégories runtime à `0x800C5F7C+8i+6` donnent : armes 1 à 10, 14 et 16 ; armures 17 à 20 ; anneaux et bracelets 51 à 56.
7. [M] Les recensements 0x82/0x83/0x84, les coffres (Herbe 70, Potion L 15, Extrait 10, Breuvage 17, Potion S 24) et les sites 0x87 (20/6, 24/10, 6/3, 32/15, 3/2, 56/13, 41/8) sont identiques aux miens. Les 0x95 de type bombe sont 28 sites sur 28 cartes.
8. [F] Les statuts du tableau 2.2 correspondent aux titres du plan (f6b READY, g1b ⚠️, g2c1/g2b2/g4b/h0a/h3a/h4a/h4b1/h4c1/h4d1/h4e1 READY, h3b en pause, h1/h4c2/h4d2 à réviser). Il en va de même pour les tranches CONFIRMED.
9. [F] Trois orphelins sont bien sans tranche : la cape de sable (O-H-1, plan l.11158 et l.833), O-E20-24 et la Graine magique. Pour cette dernière, le plan l.8598 dit « reporté à h1 », mais h1 ne couvre que cinq objets. Le stub est en `ItemEffects.cs:100-101`.
   - Pour O-E20-24, la section f6b ne cite pas `fire11_point_blank`. La trace montre x=109 puis status 3 à S+2.
10. [F] Le coffre F255 n'a aucune section dans `plan-e14*` : un `rg` sur tout `docs/` hors `plan-e20*` ne trouve rien. Le plan l.8386 et l.8459 le renvoie à « E14 ».

## B. RÉFUTÉ ou ÉCART
1. **HEAD périmé.** HEAD = `af05ff2d`, pas `5eccf614` : quatre commits de plus (g1b T0, tests rouges, phase A).
   - `PlayerManager.cs` a reçu +78 lignes en 957-1034. Toute citation `PlayerManager.cs:` au-delà de :955 est décalée de +78.
   - Exemples : `PlayerTryAttack` est en :1054 (le rapport dit :976), `UpdateItemUseDriver` en :1677 (:1599), `ProductionWiredWeaponTypes` en :1319 (:1241).
2. **« Gant : absent, pas de `TryLift` » est partiellement faux.**
   - `TryLift` et `PlayerTryActionCore` existent en fonctions pures internes (`ownsPowerGlove`, genres 2 et 3), avec 388 lignes de `LiftPureTests`.
   - `ProductionWiredLiftableTypes` est vide et `PlayerTryAction` de production rend toujours 0 (:955). Le comportement en jeu est donc inchangé.
   - La phase A (T1) de g1b est codée, alors que le lecteur écrit « T1 en cours ».
3. **« Nibbles 8 à 11 testés par aucun programme de carte » est faux à la lettre pour 0x95.**
   - [M] La carte 226 contient un programme C[14] (pc 2113 à 2243) qui teste les indices 0, 1, 2, 4, 6, 7, 8, 9, 10 et 11. Les cartes 227, 228 et 229 y mènent par `DestMapId`.
   - [M] L'épée (indice 0) est testée en 0x95 sur les cartes 41 (2 sites), 42 (3) et 226 (1). Le lecteur ne cite que la bombe.
   - [I] La correspondance indice = type − 1 est déduite de la bombe (indice 3, nibble 4).
   - Pour 0x87, le lecteur a raison : indices 0 à 7 seulement.
4. **ATK-15 : le « [I] pour le lien causal » devient [M].**
   - Le gestionnaire 0x82 (`0x8003FE7C`) passe l'octet d'opérande à `0x80034108`. Hors des cas spéciaux, la table `0x80023234` renvoie vers `AddItem` (`0x80033EC8`).
   - Donc `0x82 [8]` donne l'objet 8. C'est l'offset 1623 du programme B[10] de la carte 7, sur la branche du dialogue 130.
5. **« Obtention par script 0x82 » est incomplète.** Une troisième voie existe : 0x8A/0x8B avec un type de 31 à 116, qui fait naître un ramassage.
   - [M] Carte 7 : type 31. Cartes 10 et 331 : types 55 à 107.
   - [M] L'objet 8 (type 38) naît sur les cartes 6, 52 et 80. L'objet 3 (type 33) naît sur les cartes 44, 61, 63, 64 et 80.
6. **Objets de script omis.**
   - #74 Bouquet de fleurs : 0x84 sur les cartes 10 et 331, 0x82 sur la 372.
   - 0x83 sur #37, #39 et #41 (cartes 327 et 482), sur #5 (231) et sur #9 (199).
7. **Wagonnet.** Le lecteur écrit « hors d'E20 ». Le plan (l.11169) dit « sans propriétaire, à désigner par l'auteur » : il n'a donc aucune tranche nulle part.
   - Les poses sont forcées par script sur 0x37 (cartes 61 à 64, 66, 68, 329), 0x1B (66) et 0x10 (451 par 0x5B, et 388 par 0x1A). La 388 est absente du rapport.

## C. MANQUES de l'inventaire
1. **Chaîne PM vers tomes, inatteignable en partie normale.**
   - [M] `MpMax` n'augmente que par le corps de #80 (`0x8003382C` appelle `0x8004DE4C`, appelant unique `0x80033848`). `SetPlayerMp` borne à `MpMax`. Le départ des tomes exige des PM non nuls, sauf avec le Bâton magique.
   - [F] `MpMax` initial vaut 0 (`PlayerStats.cs:49-50`). Le corps de #80 est un stub.
   - [M] #80 n'existe qu'en coffres (cartes 7, 15, 25, 434), sans 0x82 ni spawn. Le coffre n'a pas de tranche.
   - [I] Sans coffre ni corps de #80, h4a à h4f2 et les cristaux ne se jouent que sur sauvegardes de test (presets `MpMax 4`) ou avec le Bâton magique (#7, donné par 0x82 sur la seule carte 134). Cette dépendance manque au tableau §4.
2. **Coffre F255, ampleur.**
   - [M] 471 coffres hors carte 0, dont 400 avec contenu, sur 158 cartes.
   - [M] Objets dont la seule source hors carte 0 est un coffre : #54 Bracelet d'argent (carte 303), #69 (cartes 148 et 150), #80 (cartes 7, 15, 25, 434).
3. **Cape de sable, mécanisme non décrit.**
   - [M] Le départ `0x80034320` exige `[0x80127164]==0`, le terrain du héros `+0x18C == 3` et l'état `< 2`, puis passe à l'état 0x20.
   - [M] La sortie exige l'état 0x22, le héros au sol, `+0xE4` et `+0xE8` à 0, et aucune entité dans la boîte. Elle donne l'état 0x21. Sinon : son 3.
   - [M] La Cape aquatique #40 n'a aucune fonction au Rond (refus).
4. **Sélection par créneau.**
   - [M] `0x8004E18C` prend l'objet possédé de rang maximal pour les créneaux à drapeau 1.
   - [I] #8 (créneau 1, rang 4) l'emporte donc sur Elnard (rang 3). Il a les animations de l'épée de base (enregistrement 11), sans charge ni onde. Le posséder dégrade le coup tout en ajoutant +50. D-E20-45 ne le dit pas.
5. **Scripts qui lisent l'état des tomes.** [M] Les cartes 10 et 331 testent `0xB8 [129, 50/51/52]`, soit les animations 0x32 à 0x34. h4a doit les produire pour que ces programmes se comportent.
6. **Objets sans effet.** [M] #21 à #24, #29, #30 et #58 sont de catégorie 0 (sans effet). #23 et #24 n'ont aucune source hors carte 0. Le lecteur ne cite que #11, #12, #13 et #15, qui sont aussi de catégorie 0.

## D. NON ÉTABLI
1. Le rôle réel de la carte 226 (test ou énigme).
2. La carte 421 force les animations 0x02 à 0x09 et 0x0B via 0x1A. L'heuristique de contexte 0x42 est trop grossière pour conclure.
3. Si la passe de coups générique de la DLL applique déjà la ligne P100 aux scènes du wagonnet.
4. Les citations DLL hors `PlayerManager.cs` ne sont revérifiées que pour : le registre à 8 classes, `ItemEffects.cs:100-101`, `WeaponTables.cs:86`, `WaveSwordAi` et `TileEffect`, `EventProgramRunner.cs:3140-3172`, les bottes dans `ScriptedMotion.cs`.

## Verdict
L'inventaire est exact sur le binaire, les données et les statuts du plan : aucun élément d'attaque absent côté binaire, aucune erreur de table. Il pèche sur cinq points :
- le HEAD périmé et les lignes décalées de +78 ;
- `TryLift` et le gant présentés comme absents ;
- une affirmation fausse sur les types d'attaque testés par 0x95 ;
- des voies d'obtention manquantes (spawn de ramassages, #74) ;
- l'absence de la dépendance chaîne PM, coffre et #80.

Le coffre F255 est le trou majeur : aucune tranche, 400 coffres, et il bloque la Graine magique, le Bracelet d'argent et l'accès normal aux tomes.