# E20.e0c, T1 (phase A) : résultats (2026-10-22)

Base : HEAD `84f140b9` (deux commits de T0 sous `32cd82ad`). Compte de départ de la suite : 5 098 tests en Release, tous verts.

## Ce qui est livré, sans changement de comportement

Fichiers neufs de la DLL : `Alundra/Items/LootDrop.cs` (`Veto` `0x80032A00`, `SpawnRandomLoot` `0x80032C7C`, `SpawnContents` `0x80032B90`), `Alundra/Items/ItemEffects.cs` (`GiveItem` `0x80033F00`,
`ComposePickupMessage`), `Alundra/Hero/HeroHeals.cs` (neuf corps de soin : `SmallHeart`, `MediumHeart`, `LargeHeart`, `Herb`, `LargePotion`, `SmallPotion`, `MagicDrop`, `LargeCrystal`, `Brew`),
`Alundra/Ai/Objects/PickupAi.cs` (les accès 32 bits propres à e0c, D-E0C-13). Modifiés, de façon additive : `Alundra/Items/ItemTables.cs` (`DropField1` colonne 0, `DropSoundSfxIndex` colonne 1),
`Alundra/Rendering/Effects/NativeEffects.cs` (`CreateHealGlow`, `CreateHealSpark`, `CreateHealRing`), `Alundra/Dialogues/DialogueDirector.cs` (`OpenComposed`, plus un `BeginOpening` privé que `Open`
et `OpenComposed` partagent : mêmes instructions, même ordre), `Alundra/Maps/IEntityWorldContext.cs` (`Block` et `Unblock`, membres par défaut sans effet), `Alundra/Maps/WorldProxy.cs` (leur implémentation).
Rien n'appelle ces membres en production : aucun comportement ne change. `Alundra/Entities/EntityScriptProxy.cs`, le moteur, le convertisseur et les données : sans diff.

Tests neufs : `Items/LootDropTests.cs` (l1), `Items/ItemEffectsTests.cs` (l2, l5), `Items/ItemTablesTests.cs` (3 tests ajoutés, l3), `Ai/Objects/PickupAiTests.cs` (l4), `Hero/HeroHealsTests.cs`,
`Dialogues/DialogueDirectorTests.cs` (l5), `Maps/WorldProxyBlockTests.cs` (l6), et deux aides `Support/AnnexFiles.cs`, `Support/PickupHost.cs`. 60 tests dans les sept classes, dont 5 d'`ItemTablesTests` qui existaient.

## Rouge avant, vert après

- Squelette qui compile (mêmes signatures, corps vides : colonnes à zéro, `LootDrop` qui rend faux, `GiveItem` qui rend 0, accesseurs à zéro, créateurs d'effets vides, `OpenComposed`, `Block` et `Unblock` vides) :
  57 tests des sept classes, **48 rouges**, 9 verts (les 5 anciens d'`ItemTablesTests`, la garde de données du compte des vecteurs de `LootDropTests`, les deux gardes de données d'`ItemEffectsTests`
  [228 vecteurs de 19 objets, maxima d'inventaire de la table réelle = ceux du binaire], et la ligne `0` de la théorie `EachWord_RoundTrips…`). Les noms des 48 rouges : `t1-red-names.txt`.
- Quatre tests d'`ItemEffectsTests` ont été réécrits après ce passage (états explicites au lieu de vecteurs filtrés, seuils chiffrés sur le fichier de vecteurs) : leur rouge est refait par une mutation
  réelle (script, annulée, octet pour octet) qui rend `GiveItem` et `ComposePickupMessage` inertes : 17 des 19 tests d'`ItemEffectsTests` rouges, les deux gardes de données restent verts
  (`t1-red-names-itemeffects-final.txt`).
- Code de la phase A : **60 tests verts sur 60** dans les sept classes.

## Suites complètes

| suite | tests | échecs |
|---|---|---|
| base (HEAD `916f0374`), Release, copie neutralisée | 5 098 | 0 |
| après T1, Release, copie neutralisée | 5 153 | 0 |
| après T1, Debug (en dernier) | 5 153 | 0 |

5 153 = 5 098 + 55 tests neufs (60 moins les 5 qui existaient). Aucun test existant ne bouge : le compte prédit en T0 (e) pour la phase A (zéro) est confirmé. `docs/hero-trace-389-*.txt`
réécrits par les suites (fins de ligne) : vérifié `diff --ignore-cr-at-eol` vide, puis restaurés. `cmp` : `alundra-project/Alundra.dll` identique à `Alundra/bin/Debug/net9.0-windows/Alundra.dll`.
`python -B tools/dll-structure/check_names.py --rules` : 0 constat, 1 avertissement (`TextBoxGpu.cs:266`, celui de départ).

## Décisions de la tranche et faits

1. **Z des effets.** Les nouveaux créateurs et la branche du vase de `GiveItem` reçoivent le Z du binaire (`PosZ` de la DLL plus une unité brute), comme les clés de l'oracle. `UseLifeVessel` de l'opcode `0x82`
   passe `PosZ` tel quel : une unité brute d'écart entre les deux chemins du vase, invisible. Non touché (hors périmètre).
2. **Espace du message.** `ComposePickupMessage` met l'espace de tête de l'ETC `0x45` que le compilateur Yarn a rogné (T0 (h)) : `nom + " " + "gagné!"`. Deux noms (`0x06`, `0x39`) ont un espace de fin perdu au compilé.
3. **Objet `0x4F`.** Porté en entier (le compteur `FalconTemp` plafonné à 50 et le drapeau 1450, comme l'opcode `0x82`) plutôt qu'en bouchon. **Objet `0x50`** : bouchon nommé (`0x8003382C` non lu).
4. **Objet hors de la table (98 et plus).** Aucun tirage n'en donne ; le binaire lit les octets qui suivent la table dans son image (600 pour 98, 127, 128), la DLL n'a pas de rangée : vie -1. Les trois cas
   de l'oracle sont comparés sauf sur `+0x278`, où un test dédié épingle -1.
5. **Identité du bloqueur.** `Block` et `Unblock` utilisent `OwnerEntity` de `self`, à défaut son `LogicContextEntity`. Le héros est dans la liste (en tête).
6. **Inventaire d'abord.** `0x24`, `0x25`, `0x26`, `0x27`, `0x29` : ajout puis test « compte avant < compte après ». Sans table d'objets (monde dégradé) l'objet n'entre pas et se boit sur place.
7. **Signature de trailer des commits.** La ligne `Co-Authored-By` porte le nom du modèle de la session (Sonnet 5.5), pas celui que la consigne citait.
