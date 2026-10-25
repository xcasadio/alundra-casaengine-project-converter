Rapport du critique, thème 2 : le coffre au trésor (F255) et la Graine magique #80. Lecture seule respectée, aucun build ni test. Le HEAD est maintenant `e151f997` : la tranche g1b a committé l'activation de `PlayerTryAction` après le rapport.

**Méthode.** J'ai refait mes propres outils : un désassembleur capstone, un interpréteur MIPS écrit à neuf et un recensement indépendant des données. Les scripts sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest/critic/`. Les rejeux sont `replay_f255.py`, `replay_c255.py` et `replay_seed.py` ; le recensement est `census.py`. Ils s'appuient sur `lib.py`, `xr.py` et `mips.py`, ce dernier faisant tourner le code réel du binaire.

**CONFIRMÉ**
1. F255 `0x8007FC88`, rejoué sur 12 cas avec le vrai code :
   - Les quatre gardes sont exactes : `0x800DC4B8 != 0`, `0x800DC4C4 != 0`, `hero+0x2C != 0` (objet porté), dialogue ouvert.
   - Contenu 0 ou `+0x88 != 0` donne ETC 0x48 en mode 0.
   - Le coffre passe à `+0x88 = 2`. L'objet naît au type `contenu+0x1E`, à x et y du coffre, z `+0x100000`. Ensuite viennent `Block`, l'effacement de `0x180` dans `+0x6C`, `+0x278 = 0x40`, `+0x274 = 2` et `+0x280 = coffre`.
   - `+0x288 = 1` seulement si CanTake est vrai, l'octet 5 vaut 0 et `[0x80029BCC] != 0`. Cette valeur d'image vaut 1 et n'est jamais écrite (5 lectures, 0 écriture).
   - Un échec d'apparition donne ETC 0x49 et laisse le coffre à 2.
2. CanTake `0x80033D34`, table complète rejouée : 0 pour l'objet 0 ; plafonds 9, 1, 1, 1, 1 pour `0x24`, `0x25`, `0x26`, `0x27`, `0x29` ; vrai pour tout le reste.
3. A255 et C255 de l'objet-coffre, rejeux de bout en bout :
   - Objet ordinaire : son = octet 5 (52 ou 53), boîte en mode 1, puis ApplyItem, SetGameFlag (drapeau du coffre), Unblock, destruction.
   - Sac plein : message « Tu transportes… garder X. », `chest+0x88 = 0`, ni drapeau ni ApplyItem.
   - Objet-clé (épée, graine…) : piste CD 11 dans A255, boîte en mode 1 avec masque 4, puis masque 3 et `StopAllSound`, puis ApplyItem.
4. Graine `0x8003382C`, rejouée pour MpMax de 0 à 5 et Mp variés :
   - MpMax passe à `min(MpMax+1, 4)` et Mp à MpMax ; le son est `0x36`.
   - 12 créations de l'effet 14 en animation 2 (8 anneaux plus 4 étincelles) et 8 pas du générateur ; si la création est refusée, le générateur n'avance pas.
   - Les trois entrées (GiveItem, ApplyItem, `0x80034108`) appellent le même corps. MpMax initial vaut 0 (`0x800317D0`) ; la branche à MpMax 3 est un démarrage hors nouvelle partie.
   - La graine est inatteignable par butin : la ligne 0x81 de la table n'est utilisée par aucun enregistrement ni en-tête, et aucune ligne ne s'y chaîne. Aucun `0x82 [80..82]` ni apparition de type 110 n'existe non plus. Les 4 coffres (cartes 7, 15, 25, 434) sont donc la seule source.
5. Chaîne d'ouverture :
   - Drapeaux `0x8180`, donc 0x8000 (Carré obligatoire, octet `0x80` de `0x80126F2A`). TryLift renvoie 0 (genre nul) et le pick choisit le créneau 5.
   - Le résultat 2 remet le héros à l'état 0. Les appels viennent des états 0 et 1, plus les états de nage 0x0F et 0x1D, pas de la course.
   - La seule contrainte de direction est celle du latch (`TargetDirection`).
   - Le `PlayerTryAction` de g1b rend bien 0 pour le coffre.
6. Données, recensement refait à partir de `data-extracted/data/map_*.json` : 569 enregistrements de type 30 dont 51 sur page de carte, 98 en carte 0, **420 coffres hors carte 0 sur 170 cartes**. Parmi eux : 400 avec contenu sur 158 cartes, 20 vides, 404 natifs, 16 à F scripté, 54 à C scripté.
   - 4 coffres sans drapeau (cartes 11 idx 30, 161, 362, 398), aucun drapeau en banque temporaire, drapeaux de 185 à 2000, 367 distincts, 29 partagés, 174 à plafond.
   - Les 30 cartes de chaîne n'ont aucun coffre ; les 7 cartes de combat en ont 25. Les programmes cités sont réels : carte 115 `0x84 [61]` en pc 982 et `0x9F [10]` en pc 1065, carte 117 attente du drapeau 260 en pc 1024.
7. Persistance : `InitializeContents` `0x80032A40` et l'opcode `0x9F` `0x80040C80` sont conformes au rapport. Textes ETC (0x48, 0x49, 0x4C vide, 0x4D, 0x4E, 0x4F) conformes, avec les espaces à restaurer.
8. État de la DLL : registre, bouchons `PickupAi.cs:21,70,76,118`, `ItemEffects.cs:101`, dégradé `EventProgramRunner.cs:3170`, `CheckEntityInteraction` porté branche par branche, `Block` qui épargne le coffre et les entités Loaded. Les tests épinglant le bouchon sont ceux listés.

**RÉFUTÉ ou ÉCART**
1. **D-E20-51, Q-H1-3 (B), O-H-5 et le plan l.8492 reposent sur une prémisse fausse.** Le plan dit « soin reçu d'un coffre (`0x82`) sac plein se boit sur place ».
   - `GiveItem` `0x80033F00` n'a qu'un appelant, C255 (`0x8007C598`, objets posés et lâchés).
   - L'opcode `0x82` (`0x8003FE7C`) appelle `0x80034108`, qui envoie `0x24` à `0x2A` vers `0x80033EC8` : ajout seul, aucune boisson.
   - Le coffre natif refuse l'objet dès CanTake. La DLL est donc déjà fidèle sur `0x82`.
   - Conséquence : h1 « touche l'interpréteur » ajouterait une divergence. Il faut rouvrir Q-H1-3 avant de réviser h1.
2. Question 5 du rapport, formulation fausse : `Block` n'ôte pas les entités de la liste visible, dont le constructeur (`0x800384F4`) ne teste pas `+0x20`. Il les ôte de la liste active : programmes, physique et animations (appel `0x8003B3D8`) s'arrêtent, les sprites restent dessinés. Le défaut « figer » veut dire geler les mises à jour, pas masquer. `WorldProxy.cs:3665` dit déjà que l'horloge d'animation et la physique ne lisent pas encore `BlockedByEntity`.
3. `0x800DC4C4` n'est pas « fin de partie » : c'est le champ `+4` d'une structure de demande de changement de carte à `0x800DC4C0`. Il est posé à 1 par au moins 7 sites, dont `0x80032848` avec la carte destination `0x1DD`.
4. Citations périmées : `PlayerTryAction` est désormais réel (commit `e151f997`). `TryUseItem` reste un bouchon, en `PlayerManager.cs:950`. Les numéros de lignes de `PlayerManager.cs` ont bougé.

**MANQUES**
1. 78 coffres sur 400 prennent le chemin fanfare (38 vases, 4 graines, 36 armes, tomes, clés…) : tous dépendent du bouchon E19.sm7.
2. `ItemTables` n'expose pas la colonne 4 de `item-drop-properties.json` (l'octet 7, le plafond). Il faut l'ajouter, ou lire `ItemsProperties[item*5+3]`, égal à l'octet 7 pour les 5 objets concernés.
3. Enregistrer A255 sur le type 30 dessinera ouverts les 20 coffres à contenu 0 (16 scriptés et 4 natifs de la carte 325). C'est fidèle au binaire, mais visible par rapport à la DLL, qui les dessine tous fermés. Les scripts des cartes 136 et 363 font `0x59 [129,0]` avant d'ouvrir, ce qui corrobore. À poser à l'auteur, défaut : comme l'original.
4. Carte 0 (Test Map) : le contenu 42 (type 72, sans A255 ni C255) et les contenus 89 à 97 (types 119 à 127) sont dégénérés. Aucune carte réelle n'a de contenu supérieur à 83, et le 42 n'y figure pas. La tranche doit garder un garde-fou pour la carte 0 ; je ne m'avance pas sur le résultat exact, c'est une INFÉRENCE.
5. Le z passé à l'apparition est dans le repère du binaire (`PosZ + 1`, ADR-0026) plus `0x100000`. Il faut aussi poser `BlockedByEntity = null` sur l'objet après `Block`, comme le binaire (`o+0x20 = 0`).
6. Recette : tuile = XPos/2 et YPos/2, tileZ = Height/2, vérifié sur `pickup-m11` contre l'enregistrement 22 de la carte 11. Exemples : graine carte 15 idx 4 en (35, 41, 5), clé carte 117 idx 19 en (42, 41, 2). Le préréglage `pickup-m11` a déjà MpMax 4 : la graine exige un préréglage à MpMax de 0 à 3. Le HUD suit déjà MpMax (`HudDirector.cs:493,612`).
7. Le plan renvoie le coffre à « E14 » (l.8418 et 8491), qui n'en parle pas : à corriger avec la nouvelle tranche, ainsi que O-E19-39.

**NON ÉTABLI**
- Les 127 ticks du Carré au message dépendent de l'ordre des passes de la DLL, à ±1 près. Ne pas les épingler tels quels. Les 61 ticks de l'animation 2, eux, sont reconfirmés sur les données (somme des délais) mais je ne les ai pas rejoués.
- Je n'ai pas relancé les 15 mutants du rapport.
- Les mesures T0 (a) à (f) (son 51, objet Loaded dessiné ou non, gel des animations) ne se font pas en lecture seule.
- La correspondance des noms de sauvegardes de `tools/Saves/` reste INFÉRENCE.

**Verdict.** Le rapport est exact sur le binaire, les données et le plan de tranche. Le seul vrai défaut est de ne pas avoir relevé que D-E20-51 est contredit par ses propres mesures. Il faut aussi ajouter le chemin fanfare (78 coffres) et la bascule visible des coffres vides.