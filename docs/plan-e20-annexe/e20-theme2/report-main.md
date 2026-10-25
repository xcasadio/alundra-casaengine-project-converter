# Thème 2 : attaques du joueur et utilisations des objets d'inventaire
Rapport de découverte en lecture seule. Dépôt `D:/development/repo/alundra-casaengine-project-converter`, HEAD `5eccf614`, 2026-10-24.

## 0. Méthode, sources, limites

- Aucun build ni test lancé. Aucune écriture dans le dépôt (seulement dans le scratchpad). Aucune commande git qui écrit.
- `git status` montre `Alundra/` et `Alundra.Tests/` identiques au HEAD. Les numéros de ligne cités sont donc ceux du HEAD. Seul `docs/plan-e20-annexe/e20-g1b/` est non suivi.
- Étiquettes : **[F]** fait, lu dans le plan, le code ou une annexe, source citée. **[M]** mesuré par mes scripts sur `ALUN_CD.EXE` (France) ou sur les données exportées. **[I]** inférence, non vérifiée.
- Binaire : `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE`. Les adresses sont des adresses mémoire PSX.
- Scripts, dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/`.
  - `bx.py` : lecteur et désassembleur capstone. Il ajoute explicitement le site-packages utilisateur à `sys.path`, car `python -I` l'exclut.
  - `scan.py` : sites d'appel et constantes d'arguments sur tout le binaire.
  - `t1_tables.py` : tables de départ, pilote et annulation.
  - `t2_items.py` : propriétés des objets.
  - `t3_weapons.py` : table des armes et lignes d'attaque du héros.
  - `t5_equip.py` : fiches d'équipement.
  - `t6_types.py` : sprites globaux de projectiles et de sorts.
  - `t9_itemops.py` : opcodes 0x82, 0x83, 0x84 et 0xC0 sur le corpus.
  - `t10_handlers.py` : adresses des gestionnaires natifs.
  - `t12_hitops.py` : opcodes 0x87 et 0x95.
  - `t13_chests.py` : contenus des coffres.
  - `t14_heroanim.py` : animations du héros forcées par script.
  - `t7_statewrites.py`, `t8_opcodes_items.py`, `t11_map7.py` : contrôles ponctuels.
- Limites.
  - Les recensements d'opcodes sont un balayage linéaire des octets de programme, du début de chaque programme jusqu'au suivant. 7 462 programmes, 1 arrêt sur opcode indécodable. Des faux positifs sont possibles.
  - Les comportements fins (soins, bombe, haricots, sorts) sont relayés depuis les annexes du plan, qui sont déjà contre-vérifiées. Je ne les ai pas remesurés un par un.

## 1. Liste exhaustive, tirée du binaire et des données exportées

### 1.1 Mécanique commune des armes [M]

- `PlayerTryAttack` : `0x8002EAF4`. Relâchement d'abord, puis appui. Garde `+0x180 & 0x2000`.
- `UpdatePlayerWeaponEffect` : `0x8002F49C`. Compteur de charge, aura, étincelle.
- `UpdateWeaponStepProgression` : `0x8002F768`. Il appelle `SpawnWarpEntity` `0x80039F58` avec le type du registre d'arme.
- Groupes d'états d'attaque de la table `0x80022D3C`.
  - Sol, `0x80031E18` : états 04, 08, 09, 10, 11, 12, 13, 18, 19, 25, 3F, 40, 44, 49.
  - Air, `0x80031DF8` : états 0A, 14, 15, 16, 17, 1F, 26, 41, 42, 46, 4B.
- Tables.
  - Objet vers enregistrement : `0x80028C14`.
  - Enregistrements de 13 octets : `0x80028C78`, soit 12 enregistrements. Le « 13e » est la table de drapeaux `0x80028D14`.
  - Format : `[chargeable][sol anim,type,délai,z][air ...][charge ...]`.
- Drapeaux d'animation.
  - Octet b0 (le compteur de charge compte) : animations 00-04, 0E, 29, 2A, 2B, 2C, 2D, 35, 3E.
  - Octet b1 (apparition différée) : animations 11, 13, 15, 17, 18.
- Lignes d'attaque du héros (`balance.json`, entrée 0, ligne = animation + 1, toutes avec bit d'équipement) :

| Animation | Type, attribut | Puissance |
|---|---|---|
| 04 (plaquage) | 5, 0x85 | 2 |
| 10, 14 | 1, 0x81 | 1 |
| 12, 16 | 2, 0x82 | 3 |
| 18 | 1, 0x81 | 8 |
| 19 | 2, 0x82 | 12 |
| 3F, 41 | 1, 0x81 | 2 |
| 40, 42 | 2, 0x82 | 6 |
| 43 | 2, 0x82 | 12 |
| 44, 46 | 1, 0x81 | 4 |
| 49, 4B | 1, 0x81 | 8 |
| 1A, 1B, 37 | 1, 0x81 | 100 |

- Nibbles d'attribut. [M] Ils sont lus sur les lignes d'attaque des sprites et sur l'indexation `DamageResponses[k-1]` (`Combat/BalanceRecord.cs:48`).
  - 1 épée, 2 fléau, 3 flèche, 4 bombe, 5 plaquage.
  - 6 boule de feu, 7 boule de glace, 8 terre, 9 eau, 10 feu magique, 11 vent magique.

### 1.2 Attaques

**ATK-01 à ATK-04, les épées.** [M]
- #1 Épée de base : enregistrement 0, sol `0x10`, air `0x14`, non chargeable.
- #2 Lame d'acier : enregistrement 1, sol `0x3F`, air `0x41`.
- #3 Destructeur de démons : enregistrement 2, sol `0x44`, air `0x46`.
- #4 Épée sacrée Elnard : enregistrement 3, sol `0x49`, air `0x4B`.
- Les trois dernières sont chargeables : charge `0x18`, qui apparaît après 21 ticks avec le type 27, 28 ou 29, délai 20, z 0.

**ATK-05, la charge d'épée.** [F]
- L'aura (effet 3) naît quand le compteur atteint 11 ou plus, l'étincelle (effet 0x10) à 60.
- Le relâchement lance 16 étincelles et le son `0x2B` (code `0x8002EB6C-0x8002EC88`).
- Il est perdu en l'air.

**ATK-06, l'onde de l'épée.** [M]
- Types 27, 28, 29, nommés « Épée d'onde Niv.1 à 3 » dans l'export.
- Gestionnaires : C61 `0x8007A958` (tick, ne fait que `CheckAndTriggerTileEffect` `0x8003A7B0`) et E10 `0x8007F23C` (destruction).
- Attaque 0x81, puissance 10, 20, 38. Drapeaux `0x5C`.

**ATK-07, l'arc du chasseur (#5).** [M]
- Enregistrement 8 : sol `0x13`, type 3, délai 8, z 16. Air `0x17`, type 3, délai 0, z 16. Non chargeable.
- Sprite 3 « Flèches Niv.1 » : E17 `0x8007F420`, attribut 0x83, puissance 1.

**ATK-08, l'arc de William (#6), tir normal.** [M]
- Enregistrement 9 : type 4 au sol et en l'air, délai 8. Puissance 2.
- Les sprites 5 et 6 portent aussi E17, sans attaque. [M]

**ATK-09, la flèche chargée de William.** [M]
- Charge : animation `0x13`, type 7, délai 8, z 8.
- Sprite 7 : C94 `0x8007C0D8` (tick), E1 `0x8007ED10`. Puissance 5, drapeaux `0x54` (elle perce), effet de bris 8.

**ATK-10 et ATK-11, les bâtons.** [M]
- #16 Bâton de feu : enregistrement 4. Sol `0x11` et air `0x15`, type 11, délai 10, z 32. Charge `0x11`, type 12.
- #14 Bâton des glaces flottantes : enregistrement 5. Types 14 et 15.
- Les animations de tir n'ont pas de ligne d'attaque du héros.
- Sprites 11, 12, 14, 15 : C60 `0x8007A8A0`, E9 `0x8007EF50`. Attribut 0x86 (feu) ou 0x87 (glace), puissance 4 normale et 12 chargée.
- Les sprites 10 et 13 portent aussi C60 et E9. [M]
- Un seul autre lanceur du type 12 : la Torche, E20 `0x8007F6C8`, appel à `0x8007F730`. [M]

**ATK-12, le Lancer de chaîne (#9).** [M]
- Enregistrement 6 : sol `0x12`, air `0x16`. Attaque de type 2, puissance 3.
- Rebond : `CreatePlayerAnimationEffects` `0x8002FB14` (entrée réelle `0x8002FB1C`).
  - Octets d'états `[08, 1F, 25, 26]` à `0x800228A4`, lus en `0x800304AC-0x80030508`.
  - Déclenché par `HitCounter` ou par un mur sous un coin de la boîte.

**ATK-13, Le Broyeur (#10).** [M]
- Enregistrement 7 : sol `0x40`, air `0x42`, puissance 6. Rebonds `0x25` et `0x26`.
- Charge `0x19` (type 2, puissance 12), sans apparition. 58 ticks d'après le plan.

**ATK-14, le Bâton magique (#7).** [M]
- Enregistrement 10, tous octets à zéro : animation 0, un appui ne fait que le son 3.
- Il rend les tomes gratuits (`GetItemIdFromCurrentWeapon == 7`, `0x80034924`).

**ATK-15, l'« Objet 008 » (#8).** [M]
- Enregistrement 11, mêmes animations que l'épée de base.
- Sa fiche d'équipement (entrée 38 de `balance.json`) porte une attaque de type 6, attribut 6, puissance 50. [F] Le plan annexe la dit ajoutée à tout coup à bit d'équipement ; je n'ai pas remesuré cette règle.
- Donné par le script 0x82 `[8]` sur la carte 7 (Overworld 1,2), à la fin d'une scène à dialogue. [M] L'opérande est lu comme un identifiant d'objet d'après le nom de l'opcode ; [I] pour le lien causal.
- Les objets #11, #12, #13 et #15 sont inutilisés. Ils pointent vers l'enregistrement 0. [M]

**ATK-16, le plaquage de course (état 0x04).** [M]
- Entré depuis le gestionnaire de course : `andi 0xE0` en `0x80032104`, `li 4` en `0x80032114`. Les boutons Carré, Croix ou Rond tenus déclenchent le plaquage.
- Il appartient au groupe d'attaque au sol. Ligne héros type 5, puissance 2.

**ATK-17, la coupe d'herbe.** [M]
- `CheckAndTriggerTileEffect` `0x8003A7B0`, avec quatre appelants (plan §1.25) : le héros (`0x8002FDC0`, dans les états d'attaque), C61, C94 et la bombe E12 (`0x8007F2D4`).

**ATK-18, l'objet lancé** (caisse, jarre, tonneau, pierre). [F]
- `PlayerTryAction` `0x8002EEAC`, `TryLift` `0x8002EDBC`, `UpdatePlayerCarriedEntity` `0x8002F884`.
- C23 `0x8006B848` (tick), C4 `0x80066984`, E2 `0x8007ED30` (destruction à l'impact).
- Un objet lancé blesse une cible de classe A (O-E20-19, O-G-9 tranchées).

**ATK-19, la bombe (#35, sprite 2).** [M]
- C62 `0x8007A978`, D30 `0x8007E79C`, E12 `0x8007F27C`.
- Attribut 4, puissance 5, drapeaux `0x4E`.
- 3 bombes au plus en jeu, d'après le plan §1.30 point 4 [F].
- Les couvercles 241 à 243 attendent le gestionnaire `(E, 19)` `0x8007F690`. [F]

**ATK-20, les haricots de Jack (#31, sprite 1).** [M]
- C73 `0x8007BB30`, E21 `0x8007F7CC`, effet de bris 8. Aucune ligne d'attaque.
- Seul usage : réveiller le pot magique (type `0x199`). Le plan constate qu'il n'en existe aucun sur la chaîne d'histoire. [F]

**ATK-21 à ATK-28, les huit tomes.** Sprites et gestionnaires mesurés dans `map_alundra.json`.

| Objet | Sprite | Gestionnaires | Attaque |
|---|---|---|---|
| #43 Terre (haut) | 16 | E3 `0x8007EDA0` | attribut 8, puissance 32 |
| #44 Terre (bas) | 17 | E13 `0x8007F30C` | attribut 8, puissance 64 |
| #45 et #46 Eau | 19 | A254 `0x80061BCC`, C75 `0x8007BD8C` | attribut 9, puissance 64, effet de bris 8 |
| #47 Feu (haut) | 20 (E1) puis 23 (C63 `0x8007AC60`, E3) | | attribut 10, puissance 64, puis 32 |
| #48 Feu (bas) | 21 | E28 `0x8007FB58` | attribut 10, puissance 128 |
| #49 Vent (haut) | 22, deux apparitions | E1 | attribut 11, puissance 32 |
| #50 Vent (bas) | 24 | A254, C65 `0x8007AF20`, E1 | attribut 11, puissance 128 |

- Départ commun : `0x800348E0`. Conditions vérifiées au désassemblage : rien porté, état `< 2`, rien en cours.
  - Sauf avec le bâton magique, il faut des PM non nuls (`0x8004DEA4`), puis `DecreaseMp(1)` (`0x8004DF3C`).
  - Piste XA (`0x8005A9E0`, octet à `0x80023365 + objet`), état `0x32`. Rend 0.
- Aucun tome ne se consomme. [M]

**ATK-29, le feu de la Torche.** [M] E20 `0x8007F6C8` fait naître le type 12 (voir ATK-10).

**ATK-30, les animations d'attaque sans producteur dans la table d'armes.** [M]
- 0x1A (30 images, 29 boîtes), 0x1B (8 images, 7 boîtes, son 12) et 0x37 (8 images, 7 boîtes, son 229) ont une ligne P100 et un état par défaut (`0x80032758`, qui annule l'objet).
- Scripts qui forcent le héros : 0x37 par 0x5B sur les cartes 61, 62, 63, 64, 66, 68 et 329 (Mine de charbon). 0x1B sur la carte 66. 0x10 sur la carte 451.
- [I] Ce sont des poses du wagonnet. Le sprite 26 « Chariot de mine » porte une attaque de type 1, puissance 20. Le wagonnet est hors d'E20 (D-E20-16).
- 0x43, 0x48, 0x4D (18 images, 15 boîtes, son 36) et 0x45, 0x47, 0x4A, 0x4C (son 35) ne sont jamais choisies par la table d'armes (états par défaut).

**Réactions des décors aux attaques.** [M] Les programmes testent l'attribut par `0x87` (table de nibbles `0x80098FA4`) ou `0x95`.

| Nibble | Attaque | `0x87` : sites / cartes | Cartes `0x87` |
|---|---|---|---|
| 1 | épée | 20 / 6 | 77, 136, 304, 352, 429, 430 |
| 2 | fléau | 24 / 10 | 10, 77, 102, 136, 145, 147, 331, 352, 382, 442 |
| 3 | flèche | 6 / 3 | 16, 77, 136 |
| 4 | bombe | 32 / 15 | 6, 14, 61, 63, 64, 67, 80, 90, 104, 151, 154, 352, 362, 382, 429 |
| 5 | plaquage | 3 / 2 | 77, 136 |
| 6 | boule de feu | 56 / 13 | 13, 77, 92, 110, 137, 303, 337, 343, 347, 443, 444, 446, 448 |
| 7 | boule de glace | 41 / 8 | 77, 137, 396, 443, 444, 446, 447, 448 |

- `0x95`, type bombe : 28 cartes, à savoir 1 à 10, 12 à 17, 19, 20, 82, 84, 226, 331, 335, 407, 409, 411, 439, 479. [M]
- Les nibbles 8 à 11 (terre, eau, feu magique, vent) ne sont testés par aucun programme de carte. [M]
- Le feu, la glace et la bombe sont donc les attaques que le corpus exige le plus.

### 1.3 Objets d'inventaire avec une utilisation

Noms exportés : `EntityNames.csv`, entités `I##_*` de `alundra-project/Entities`. Colonnes de `items-properties.json` : créneau, drapeau de remplacement, rang, maximum, icône.

**Cadre du Rond.** [M]
- `TryUseItem` `0x8002ED64` : front du Rond (`0x80126F2A & 0x20`).
  - Pas de front, retourne 1.
  - `[0x800DC4B8] != 0` : son 3, retourne 1.
  - Sinon `0x8003499C`.
- `0x8003499C` : objet courant par `0x8004E0F8`. Refus (son 3, rend 1) si `>= 0x62`, si un autre objet est en cours, ou hors `0x1F-0x32`. Dispatch par la table `0x8002339C`.
- Pilote `0x8003634C` : table `0x800235F4`, appelé en `0x80031C6C` et `0x80031CC4`.
- Annulation `0x800350C0` : table `0x800233EC`.
- Tous les départs sont vérifiés par désassemblage.

| # (hexa) | Nom exporté | Départ | Pilote | Annulation | Effet mesuré |
|---|---|---|---|---|---|
| 31 (1F) | Haricots de Jack | `0x80034224` | `0x80035204` | `0x80034ACC` | en cours (`[0x80127164]`), apparition sprite 1 |
| 32 (20) | Cape de sable | `0x80034320` | `0x80035260` | défaut | états 20-24 |
| 35 (23) | Bombe explosive | `0x8003453C` | `0x800352C4` | `0x80034B54` | en cours, apparition sprite 2 |
| 36 (24) | Herbe médicinale | `0x80034680` | aucun | aucune | instantané, rend 1, consomme par `0x8004E5C4`, max 9 |
| 37 (25) | Potion L | `0x800346F0` | aucun | aucune | instantané, consomme |
| 38 (26) | Extrait magique | `0x80034760` | aucun | aucune | instantané, consomme |
| 39 (27) | Breuvage de soin | `0x800347D4` | aucun | aucune | instantané, consomme |
| 41 (29) | Potion S | `0x80034870` | aucun | aucune | instantané, consomme |
| 43 (2B) | Tome Terre haut | `0x800348E0` | `0x80035320` | défaut | sort 16 |
| 44 (2C) | Tome Terre bas | `0x800348E0` | `0x800354D0` | `0x80034BDC` | sort 17 |
| 45, 46 (2D, 2E) | Tomes Eau | `0x800348E0` | `0x80035790` | `0x80034C54` | sort 19 |
| 47 (2F) | Tome Feu haut | `0x800348E0` | `0x80035A84` | `0x80034D2C` | sorts 20 et 23 |
| 48 (30) | Tome Feu bas | `0x800348E0` | `0x80035C64` | `0x80034E08` | sort 21 |
| 49 (31) | Tome Vent haut | `0x800348E0` | `0x80035EB0` | défaut | sort 22 |
| 50 (32) | Tome Vent bas | `0x800348E0` | `0x80036218` | `0x80034EC4` | sort 24 |

- Refus au Rond (son 3) : #33 Armure brisée, #34 Objet 034, #40 Cape aquatique, #42 Objet 042. [M]
- Les autres effets de soin sont relayés depuis le plan et ses annexes (+max(5, PVmax/4), plein, etc.). [F]
- Le Breuvage #39 a deux autres usages. [M]
  - Auto-résurrection à la mort : `GetNumberOfItem(0x27)` en `0x80032808` dans `MovePlayer`, qui appelle `0x800347D4`.
  - Geste Start+Select tenu : `0x8002BC04`, qui consomme le Breuvage puis tue le héros. Non porté, D-E20-50.

**Effets passifs d'équipement.** `UpdateItemEffectState` `0x800307E8`. [M] sauf mention.
- Armures #17 à #20, catégorie 2 : réponses 16, 12, 8, 4 (toutes types), soit des multiplicateurs de dégâts de 16/16, 12/16, 8/16 et 4/16.
- Anneaux d'orc (#51) et d'ogre (#52), catégorie 3 : attaque de fiche type 1, attribut 1, puissance 3 chacun.
- Bracelet d'acier #53 : réponses 15. Bracelet d'argent #54 : réponses 15, avec 0x80 (immunité) au nibble 6, donc le feu.
- Anneaux de régénération #55 et de guérison #56 : période de régénération 200 (champ PV max de la fiche).
- Bottes : #26 niveau 1, #27 niveau 2, #28 niveau 3. #25 ne compte pas.
  - `0x80031AD0` (`< 3`) : blessure des cases dangereuses.
  - `0x800368BC` (`> 0`) : gravité, drapeau de case `0x10`.
  - `0x800369E0` (`> 0`) : ralentissement en eau peu profonde, drapeau `0x08`.
  - `0x800374FC` (`< 2`) : mur d'eau profonde.
- Gant de puissance #59 : `GetNumberOfItem(0x3B)` en `0x8002EDF8` dans `TryLift`. Les genres 2 et 3 ne se soulèvent qu'avec lui. Le genre 3 est la « Pierre très lourde ».

**Objets de script** (opcodes 0x82 donner, 0x83 tester, 0x84 consommer). [M]
- #61 Clé : consommée sur 35 cartes, testée sur 16.
- Emblèmes #62 à #68 : cartes 5 et 479.
- #60 Clé d'ascenseur, #73 Vieille clé, #75 Clé d'Anzes, #77 Orbe, #78 Pierre de Zolist, #87 Cadeau, #88 Livre d'Elna, #89 : consommés par 0x84.
- #33 Armure brisée (carte 398), #57 Billet (carte 394) et #40 Cape aquatique (carte 7, test 0x83) : seulement testés ou consommés par script.

**Objets de ramassage.** C255 `0x8007C174`, seul appelant de `GiveItem` `0x80033F00`. [F]
- Gildas #69 à #72, Bec #79, Petit cristal #81, Grand cristal #82, Vase de vie #83, Cœurs #84 à #86.
- **Graine magique #80 (0x50).** [M] Corps `0x8003382C` : `SetPlayerMpMax(MpMax+1)` (via `0x8004DE4C`, plafond 4), PM remplis, son `0x36`, effets d'anneau.
  - Elle est dans les coffres des cartes 7, 15, 25 et 434. [M]
  - Aucun script 0x82 ne la donne. [M]

**Coffre au trésor.** Sprite 30, A255 `0x80061BD4`, F255 `0x8007FC88`. Le contenu est dans le champ `Contents` de l'entité. [M]
- Les coffres contiennent de nombreux soins : Herbe sur 70 cartes, Potion L sur 15, Extrait sur 10, Breuvage sur 17, Potion S sur 24 (cartes d'essai comprises).
- Les coffres contiennent aussi des armes et des tomes sur des cartes réelles. [M] Exemples : Lame d'acier cartes 30 et 158, Elnard cartes 17 et 439, Tome de terre bas cartes 17 et 439, Tome de feu haut cartes 84 et 411.
- La carte 0 « Test Map » en contient un de chaque objet. [M]

**Obtention par script 0x82.** [M]
- Armes : #1 carte 31 ; #2 cartes 80 et 158 ; #4 cartes 17, 138, 439 ; #5 cartes 31, 77, 80, 158, 231 ; #6 cartes 17, 439, 443 ; #7 carte 134 ; #8 carte 7 ; #9 cartes 31, 77, 80, 158, 199 ; #10 cartes 17, 270, 439, 443 ; #14 cartes 17, 31, 439, 443 ; #16 cartes 17, 31, 158, 439, 443.
- Divers : #35 bombe sur 11 cartes, dont la carte 10 (programme B[8]) ; #31 haricots cartes 17, 144, 439 ; #32 cape de sable cartes 17, 31, 80, 158, 439 ; #59 gant cartes 158 et 249.
- Le Demi-Buster #3 n'est donné par 0x82 nulle part. [M] Il est dans des coffres des cartes 0 et 161, donc obtenable en jeu.

## 2. État de la DLL au HEAD et tranches du plan

### 2.1 État de la DLL par élément

Statuts : porté, partiel, bouchon, absent.

**Attaques et armes**

| Élément | État DLL | Fichier:ligne |
|---|---|---|
| Épée, trois épées, sol et air (ATK-01 à 04) | porté | `Hero/PlayerManager.cs:976` (`PlayerTryAttack`), `Items/WeaponTables.cs:48` ; lignes héros par données |
| Charge, aura, relâchement (ATK-05) | porté | `PlayerManager.cs:1017`, `:1069`, `:1140`, `:1164` |
| Onde de l'épée (ATK-06) | porté, coupe d'herbe réelle | `Ai/Projectiles/WaveSwordAi.cs:30-45` ; types câblés `PlayerManager.cs:1241` |
| Arcs, flèche chargée (ATK-07 à 09) | porté | `ArrowAi.cs:44-48`, `ChargedArrowAi.cs:37-54` ; types câblés `{3,4,7,27,28,29}` |
| Bâtons (ATK-10, 11) | absent en production | pas de `BallAi.cs`, types 11, 12, 14, 15 non câblés |
| Fléau, Broyeur, rebonds, charge 0x19 (ATK-12, 13) | porté | `Hero/PlayerAnimationEffects.cs:106` ; test `HeroChargeTests.cs:475-494` |
| Bâton magique (ATK-14) | porté (animation 0) | `WeaponTables.cs` enregistrement 10 |
| Objet 008 (ATK-15) | partiel [I] | `WeaponTables.cs:86` (`table[8] = 11`) ; aucun test |
| Plaquage (ATK-16) | porté | `PlayerManager.cs:118`, `:1042` |
| Coupe d'herbe (ATK-17) | porté | `Cells/TileEffect.cs:64`, `PlayerAnimationEffects.cs:220` |
| Objet lancé, caisse (ATK-18) | porté mais inatteignable | `Hero/CarryRules.cs:128/198/232/263`, `PlayerManager.cs:1306`, `Ai/Objects/LiftableAi.cs:60/83` ; déclencheur `PlayerTryAction` (`:955`) = bouchon |
| Bombe, haricots, tomes, Torche (ATK-19 à 29) | absent | aucun gestionnaire C62, D30, E12, C73, E21, C63, C65, C75, E3, E13, E28, C70, E20, `(E,19)` |
| Poses P100 et animations 0x43 etc. (ATK-30) | absent | hors périmètre |
| Effets de coup 4 et 5, réactions de leviers | porté | `Combat/HitPass.cs:88`, `:154-159` ; `Events/EventProgramRunner.cs:801/813/831` |

**Objets**

| Élément | État DLL | Fichier:ligne |
|---|---|---|
| Rond, cadre complet | bouchon | `PlayerManager.cs:947` (`TryUseItem` rend 1 sans son), `:1599` (pilote vide), `:755` (`CancelItemMode` vide) |
| Soins au Rond (#36-39, #41) | absent | mêmes bouchons |
| Soins à l'usage sur place d'un objet ramassé sac plein | porté | `Hero/HeroHeals.cs:23-85`, `Items/ItemEffects.cs:39-130` |
| Haricots, cape de sable, bombe, tomes au Rond | absent | pas de `DecreaseMp`, pas d'état 0x20-0x24 ni 0x32-0x34 |
| Armures, anneaux, bracelets | porté | `PlayerManager.cs:1391/1442/1512/1571` |
| Bottes | partiel | gravité et eau peu profonde `Physics/ScriptedMotion.cs:319`, `:765`, `:1002` ; mur d'eau profonde et blessure : absents |
| Gant de puissance | absent | pas de `TryLift` |
| Objets de script 0x82, 0x83, 0x84, 0xC0, 0xC1 | porté | `EventProgramRunner.cs:1522-1532`, `:646`, `:673` |
| Voie opcode 0x82 pour 0x50-0x52 et 0x54-0x56 | dégradé | `EventProgramRunner.cs:3170-3172`, O-E19-39 |
| Ramassage C255, Graine magique #80 | bouchon | `Ai/Objects/PickupAi.cs:65-66` ; `Items/ItemEffects.cs:100-101` : « NAMED STUB » |
| Coffre F255 | absent | `PickupAi.cs:16-21` : « the chest (not ported) » ; aucune clé `(F,255)` |
| Registre des gestionnaires natifs | 8 classes | `Ai/NativeSpriteRegistry.cs:35-47` (Beannoïde, Muruta, onde, ramassage, flèche chargée, flèche, pilier, soulevables et bris) |

Le héros ne peut pas être blessé : l'entrée `0x80031A68` est absente (E20.c2b non faite). L'effet des armures et des anneaux défensifs est donc invisible en jeu. Le Rond ne joue même pas le son 3 (`PlayerManager.cs:947`).

### 2.2 Tranches du plan portant ces éléments

Statuts du plan `docs/plan-e20-heros.md` : fait et CONFIRMED, recette en attente (🧪), READY, esquisse, bloquée.

| Tranche | Contenu | Statut |
|---|---|---|
| E20.b | épées, `PlayerTryAttack` | fait, recette validée |
| E20.c1a, c1b-1, c1b-2 | passe de coups, dégâts, réactions de leviers | fait, recette en attente |
| E20.f0 | sauvegardes de test d'armes, opcodes 0xC0/0xC1 | fait, recette en attente |
| E20.f1a, f1b | charge, aura | fait, recette en attente |
| E20.f2 | fléau et rebond | fait, recette en attente |
| E20.f3 (a, b, c) | direction du tir, effets de coup 4 et 5, onde | fait, recette en attente (f3c) |
| E20.f4 | flèche chargée, C94, E1 | fait et CONFIRMED le 2026-10-23 |
| E20.f5 | flèches, E17 | fait et CONFIRMED le 2026-10-23 |
| E20.f6a | attributs de tuile des entités marquées | fait et CONFIRMED le 2026-10-24 |
| **E20.f6b** | C60, E9, flammes de la 309, branchement 11, 12, 14, 15 | **READY**, exécutable (aucun prérequis manquant) |
| E20.d1, d2a | course, plaquage | fait, recette en attente |
| E20.e, e0c, e0c2 | coupe d'herbe, butin, objet ramassable, fanfare | fait et CONFIRMED |
| E20.g1a, g2a, g2b, g3, g4a, g5 | porter, lancer, briser, solidité, barricade par saut, vrais pushables | fait et CONFIRMED |
| **E20.g1b** | soulever (`TryLift`, `PlayerTryAction`, gant) | **bloquée**, T0 fait, T1 en cours, T2-T4 attendent Q-G1B-1 et Q-G1B-2 |
| **E20.g2c1** | C4 (tonneau, pierre, tronc, objets de plaque) | READY, après g1b |
| **E20.g2b2** | trous (O-G-2) | READY, après g2c1 |
| **E20.g2c2** | torches (C70, E20) | esquisse, après g1b et f6b |
| **E20.g4b** | barricade 392 en soulevant | READY, après g1b |
| **E20.A362** | arc de test du couvercle 362 face à la bombe d'Olen | ligne d'enveloppe seulement, avant h3a |
| **E20.h0a** | cadre du Rond, refus son 3, annulation | READY |
| **E20.h1** | cinq soins | READY puis « à réviser » (D-E20-50, D-E20-51) |
| **E20.h3a** | D30, E12, `(E,19)`, couvercles | READY |
| **E20.h3b** | bombe dans les mains | révision 3, en pause (Q-H3B-R), prérequis c2b ajouté |
| **E20.h3c, h3d** | haricots, pot magique | reportées (Q-H3-3) |
| **E20.h3e** | arcs bombe | esquisse |
| **E20.h4a** | départ commun des tomes | READY |
| **E20.h4b1** | requête de cibles | READY |
| **E20.h4c1** | terre 1 | READY |
| **E20.h4c2** | terre 2 | READY puis « à réviser » (D-E20-56) |
| **E20.h4d1** | feu 2 | READY |
| **E20.h4d2** | feu 1 | READY puis « à réviser » (D-E20-55, D-E20-57) |
| **E20.h4e1** | vent 1 | READY |
| **E20.h4e2, h4f1, h4f2** | vent 2, eau 1, eau 2 | esquisses |
| **E20.i2** | mur d'eau profonde, courants, poussée `+0xCC/+0xD0` | ligne d'enveloppe seulement |
| **E20.c2b** | blessure du héros | ligne d'enveloppe seulement |
| E20.e2 | pas et poussière | ligne d'enveloppe seulement (hors attaques) |

### 2.3 Éléments sans tranche

- **Cape de sable** (#32, états 0x20-0x24, pilote `0x80035260`) : aucune tranche (O-H-1 « à placer »).
- **Objet 008** : D-E20-45 « porté », tranche « à placer avec les armes ». Aucune ligne n'existe.
- **Coffre F255** : renvoyé à « E14 » (`PickupAi.cs:16-21`, plan §1.26). Je n'ai trouvé aucune tranche E14 qui le nomme.
- **Graine magique #80** (corps `0x8003382C`) : « reporté à h1 » (plan ligne 8559), mais le texte d'h1 ne couvre que cinq objets.
- **Poses 0x1A, 0x1B, 0x37** et animations 0x43 etc. : aucune tranche. Wagonnet hors E20.
- **Auto-résurrection du Breuvage** (`0x80032808`) : E18.
- **Blessure par cases dangereuses et mur d'eau profonde** (bottes) : E20.i2 (O-E19-22 placé en i2). L'entrée de blessure elle-même relève d'E20.c2b.

## 3. Dépendances et manques du moteur

### 3.1 Dépendances entre tranches restantes

- f6b ne manque d'aucun prérequis de données (f6a, f4, f5, f3, e0b1, e0k1-3 faits). Elle précède g2c2 : D-E20-64, la Torche fait naître le type 12 dont les gestionnaires sont ceux de f6b.
- g1b précède g2c1, g4b et g2c2. g2c1 précède g2b2. g1b dépend de g1a, g2a, g2b, g3 et g4a, tous faits.
- g1b est bloquée sur Q-G1B-1 (le lâcher de la marche glisse d'un demi-pas, environ 1,2 px, dans l'original) et Q-G1B-2 (contact un tick plus tôt sur une caisse). Q-G1B-3, la carte de recette 72, n'est pas bloquante. Toute la chaîne g2c1, g2b2, g4b, g2c2 attend donc g1b.
- h0a précède h1, h4a (donc tous les h4) et h3b.
- A362 précède h3a. h3a précède h3b.
- h3b exige aussi h0a, g1a, g2a, f5, f6a, et **E20.c2b** (D-E20-52). c2b n'a pas de section détaillée ; E14.a est faite.
- h4a précède h4b1, h4c1, h4c2 et h4d1. h4b1 précède h4d2, h4e1, h4e2 et h4f1. h4c1 précède h4d2.
- h1 et h4f1 précèdent h4f2.
- E20.i2 précède h4e2 et h4f1 (D-E20-61).
- L'ordre ferme du plan place f6b avant h0 et h1 (critique C-16). C'est un ordre, pas une dépendance de données.
- E19.sm7 (pistes XA) remplace les bouchons journalisés des tomes et de la fanfare.
- O-E20-24 : une flèche tirée à bout portant reste figée dans le mur. Le défaut est le bornage du premier pas d'une entité marquée dans le déplaceur (e0a3b, e0k).
  - Il n'est placé dans aucune tranche.
  - [M] L'oracle d'une boule de feu à 12 px d'un mur (`docs/plan-e20-annexe/e20-f4f6/bin/out/traces/fire11_point_blank.txt`) la montre bornée à 109 puis en éclatement dès S+2.
  - [I] Le plan de f6b n'utilise pas cette clé ; le défaut joue probablement aussi pour les boules et les sorts.
- Q-G1B-1 et O-E19-28 / Q-G3-d (glissement de la DLL contre division conjointe du pas) ont possiblement une cause commune. [I]

### 3.2 Manques du moteur déjà consignés

- Aucune tranche de ce thème ne déclare de besoin moteur ; « un besoin de toucher au moteur » est une condition d'arrêt dans leurs textes.
- Audio : G1 (pas de key-off avec relâchement par tonalité) et G2 (le mixeur reboucle sur le clip entier, sans `loop_start`/`loop_end`), dans `docs/plan-e20-annexe/e20-g5/g5c/engine-gaps.md`.
- Pistes XA, dont le départ des tomes : le convertisseur et la DLL les traitent à E19.sm7 (O-E19-36, ADR-0076).
- O-E19-40 : aimantation de descente de 4 px côté moteur contre 3 px dans le binaire. Marqué « plus tard (moteur) ».
- O-E19-42 / O-E19-28 : la division conjointe du pas demanderait un plan et un ADR du moteur. Glissement gardé (D-E19-58, Q-G3-d (A) confirmé le 2026-10-22).
- Les gaps MGUI (`CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md`) ne touchent pas ce thème.

## 4. Tableau de synthèse

Élément, binaire, DLL, tranche, statut, ce qui manque.

| Élément | Binaire | DLL | Tranche | Statut | Manque |
|---|---|---|---|---|---|
| Épée de base, sol et air | `0x8002EAF4`, états 10/14 | porté `PlayerManager.cs:976` | E20.b | fait, recette validée | rien |
| Lame d'acier, Destructeur, Elnard | mêmes, états 3F/41, 44/46, 49/4B | porté | E20.b | fait | rien |
| Charge d'épée, aura, relâchement | `0x8002F49C`, état 18 | porté `:1017`, `:1069` | E20.f1a, f1b | fait, recette en attente | recette |
| Onde Niv.1-3 | C61 `0x8007A958`, E10 `0x8007F23C` | porté `WaveSwordAi.cs:30` | E20.f3c, e | fait, recette en attente | recette |
| Arc du chasseur | E17 `0x8007F420`, type 3 | porté `ArrowAi.cs:44` | E20.f5 | fait | O-E20-24 |
| Arc de William | E17, type 4 | porté | E20.f5 | fait | O-E20-24 |
| Flèche chargée | C94 `0x8007C0D8`, E1, type 7 | porté `ChargedArrowAi.cs:37` | E20.f4 | fait | rien |
| Bâton de feu (boule normale et chargée) | C60 `0x8007A8A0`, E9 `0x8007EF50`, types 11/12 | absent | E20.f6b | READY, à faire | C60, E9, branchement, O-E20-24 |
| Bâton des glaces (boule normale et chargée) | idem, types 14/15 | absent | E20.f6b | READY, à faire | idem |
| Lancer de chaîne et rebond | état 12/16, `0x8002FB14` bloc `0x800304AC-0x80030508` | porté `PlayerAnimationEffects.cs:106` | E20.f2 | fait, recette en attente | recette |
| Le Broyeur, rebond, charge 0x19 | états 40/42, 25/26, 19 | porté | E20.f1a, f2 | fait, recette en attente | recette |
| Bâton magique | enregistrement 10 | porté (animation 0) | E20.b | fait | sorts : voir tomes |
| Objet 008 (+50) | enregistrement 11, fiche item+0x1E | partiel [I], par les données | aucune (D-E20-45) | pas de tranche | tranche à placer, test absent |
| Plaquage de course (état 04) | `0x80032104`, ligne type 5 | porté `:118`, `:1042` | E20.d1 | fait, recette en attente | recette |
| Coupe d'herbe | `0x8003A7B0` | porté `Cells/TileEffect.cs:64` | E20.e | fait | rien |
| Objet lancé (caisse, jarre) | `0x8002EEAC`, C23, E2 | porté mais inatteignable | g2a, g2b fait ; g1b bloquée | **bloquée** | `TryLift`, `PlayerTryAction` |
| Tonneau, pierre, tronc, objets de plaque | C4 `0x80066984` | absent | g2c1 | READY | après g1b |
| Lancer dans un trou | `0x8003A208-0x8003A25C` | absent | g2b2 | READY | après g2c1 |
| Barricade 392 en soulevant | `TryLift` | saut fait, soulever absent | g4a fait ; g4b | READY | après g1b |
| Feu de la Torche | E20 `0x8007F6C8`, C70 | absent | g2c2 | esquisse | f6b et g1b |
| Bombe (#35) | `0x8003453C`, C62, D30, E12 | absent | h3a, h3b, A362 | READY / en pause | h0a, c2b, `(E,19)` |
| Haricots (#31) | `0x80034224`, C73, E21 | absent | h3c | reportée | pot magique |
| Tomes terre (#43, #44) | `0x80035320`, `0x800354D0`, E3, E13 | absent | h4c1, h4c2 | READY / à réviser | h4a, h4b1 |
| Tomes feu (#47, #48) | `0x80035A84`, `0x80035C64`, C63, E28 | absent | h4d2, h4d1 | à réviser / READY | h4b1, h4c1 |
| Tome vent haut (#49) | `0x80035EB0` | absent | h4e1 | READY | h4b1 |
| Tome vent bas (#50) | `0x80036218`, C65 | absent | h4e2 | esquisse | E20.i2, tables 256 |
| Tomes eau (#45, #46) | `0x80035790`, C75 | absent | h4f1, h4f2 | esquisses | E20.i2, h1 |
| Cadre du Rond (refus, annulation) | `0x8002ED64`, `0x8003499C`, `0x8003634C`, `0x800350C0` | bouchon `:947`, `:1599`, `:755` | h0a | READY | tout le cadre |
| Soins au Rond (#36-39, #41) | `0x80034680` à `0x80034870` | absent | h1 | READY puis à réviser | D-E20-50/51 |
| Soin sur place (sac plein) | `0x80033F00` | porté `ItemEffects.cs:39` | e0c | fait | coffre F255 |
| Breuvage : auto-résurrection | `0x80032808` | absent | E18 | non planifiée ici | mort du héros |
| Cape de sable (#32) | `0x80034320`, `0x80035260` | absent | aucune (O-H-1) | pas de tranche | tout |
| Refus au Rond (#33, #34, #40, #42) | `0x80034AAC` | absent | h0a | READY | son 3 |
| Armures #17-20 | `0x800307E8`, réponses 16/12/8/4 | porté `:1442` | E20.c1b-1, c2a | fait | blessure du héros (c2b) |
| Anneaux, bracelets #51-56 | `0x800307E8` | porté `:1571`, `:1512` | E20.c1b-1, c2a | fait | c2b pour la défense |
| Bottes #26-28 | `0x80031AD0`, `0x800368BC`, `0x800369E0`, `0x800374FC` | partiel `ScriptedMotion.cs:319/765` | c2a ; E20.i2 | partiel | mur d'eau profonde, blessure (i2, c2b) |
| Gant de puissance #59 | `0x8002EDF8` | absent | g1b | **bloquée** | `TryLift` |
| Clés, emblèmes, objets de script | `0x8003FEC8` (0x83), `0x8003FF34` (0x84) | porté `EventProgramRunner.cs:1527-1532` | E19.l2 | fait | rien |
| Graine magique #80 | `0x8003382C` | bouchon `ItemEffects.cs:100-101` | aucune explicite | pas de tranche | +1 PM max, PM pleins, son 0x36, effets |
| Coffre au trésor (F255) | `0x8007FC88` | absent | « E14 » sans tranche | pas de tranche | tout |
| Poses P100 0x1A/1B/37, animations 0x43 etc. | état par défaut `0x80032758` | absent | aucune (wagonnet hors E20) | pas de tranche | à confirmer [I] |

## 5. Écarts entre plan et mesures

- Le plan §1.25 « État des lieux » (2026-10-16) et l'annexe `e20-themes/attacks.md` sont périmés. Depuis, f4, f5, f6a, g1a, g2a, g2b, g3, g4a, e et e0c sont faits, et `CheckAndTriggerTileEffect` est réel.
- O-E20-19 est encore listé ouvert dans §2 alors que le plan le dit tranché (ligne 8241).
- L'objet 008 est donné par script sur la carte 7. Le plan écrit « on ignore si une carte la donne ».
- La Graine magique est obtenable en coffre (cartes 7, 15, 25, 434), et son effet n'est dans aucune tranche.
- g1b est dans l'état « bloquée » (⚠️) au HEAD : les questions à l'auteur Q-G1B-1 et Q-G1B-2 retiennent toute la chaîne g1b, g2c1, g2b2, g4b, g2c2.