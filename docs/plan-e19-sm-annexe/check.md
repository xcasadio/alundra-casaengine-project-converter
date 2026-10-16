## Critique de l'inventaire script2 (lecture seule)

**Verdict global :** fiable pour les comptes de sites sautés (510 lignes, 0x60 = 172, somme des états à 256). Pas fiable pour la fidélité. L'état « FAITHFUL » est une présence de `case`, pas une vérification au binaire, et le libellé « fidèle » est appliqué à au moins un cas où le code documente un écart. Je n'ai pas relancé les scripts ni le désassemblage (lecture seule, aucune écriture), donc les chiffres de corpus ne sont vérifiés que contre le TSV produit par l'inventaire lui-même.

**Règle native/scriptée et « fidèle » (réfutée comme preuve de fidélité)**
- `build_tsv.py:52-55` : « Everything with a case in Dispatch is FAITHFUL unless listed here ». `PARTIAL = {0x82, 0xA4, 0xAC, 0x53, 0x2F}`, et `NOOP` pour le reste des cas marqués. FAIT : FAITHFUL = présence d'un `case` par défaut.
- Conséquence : les 150 « FAITHFUL » (§1) ne sont pas vérifiés. §6 les cite pourtant comme « fidèle » avec preuves partielles.

**Vérifications par ligne**

| Affirmation | Verdict | Preuve |
|---|---|---|
| Table : 198 entrées, commentaire dit 197 | Confirmé | 198 lignes `{ 0x.., new(` dans `EventOpcodeSizeTable.cs`; `:11` dit « 197 » |
| `Dispatch` 613-2154 a 158 `case` | Non vérifiable | 164 lignes `case 0x` dans tout le fichier, autres `switch` inclus ; pas de compte par plage |
| `0x01` rend 1 (`:617`) | Confirmé | `AlundraEventProgramRunner.cs:617-618` |
| `0x29` cite `0x8003DC40` (`:736`) | Confirmé | `:736` |
| `0xB4`-`0xB6` libellés faux (`:228-230`) | Partiel | Libellés = « ForceZ < 0 », « >= 0 », « != 0 » (`EventOpcodeSizeTable.cs:228-230`). Que le binaire soit « > 0 » : non vérifié |
| `0x88`, `0xA9`, `0xAA` libellés vides (`:184`, `:217-218`) | Confirmé | `:184`, `:217-218` |
| `0xA4` partiel `:1611-1624`, cycle de palettes non porté | Confirmé | `:1611-1624`, `LogDegradedPaletteCycleOnce` |
| `0xAC` `:1305`, `0x2F` `:1864`, `0x53` `:1928`, `0x82` `:1560` | Confirmé | Lignes présentes ; `:3172` contient aussi un `case 0x53` imbriqué (sous-cas de `0x82`) |
| `0xA8` `Result = 0` `:1475` | Confirmé | `:1475` |
| `map_alundra` repli avec journal `:389-396` | Confirmé | Journal `:394` |
| `0x40` « fidèle côté script » (`:886-899`) | **Réfuté** | `:887-890` documente un écart délibéré : `v1 >= 6` ne fait rien, au lieu de l'écriture hors tableau du binaire. L'inventaire ne le mentionne pas |
| `0x40` « tous avec l'index 0 » | Ambigu | `census-op0x40.tsv` : `v1_slot` = 2 à 5, `v2_index` = 0. Le libellé de l'inventaire ne dit pas quelle colonne |
| 510 sites sautés | Confirmé | 510 lignes dans `skipped-sites.tsv` |
| `0x60` = 172 sites | Confirmé (sur le TSV seul) | 172 lignes d'opcode 60 |
| Sites de `3C 3D 15 5F 66 7A 9E B4 B7 B9` = 43 | Confirmé | Cohérent : 42 (`B9`) + 1 (`3D`) |
| Sommes 150+5+3+2+29+9+58 = 256 ; parts % | Confirmé | Arithmétique exacte |
| 157/158 opcodes ont un test nommé, `0x30` sans | Non vérifiable en totalité | Grep de `0x30` : aucun test propre, seulement des usages dans des programmes |
| « 111 136 instructions » périmé | Non vérifiable | Le chiffre existe dans `plan-conversion-totale.md` (1) et `plan-e19-opcodes.md` (2) ; pas recalculé |
| Les 9 gestionnaires binaire `0x01`, `0x14`, `0x76`, `0x77`, `0x9D`, `0xAE` | Non vérifiable | Désassemblage non relancé |

**Éléments manqués ou sous-traités**
1. `0x40` : écart documenté contre le binaire, présenté comme fidèle (voir ci-dessus).
2. Aucune trace d'un contrôle indépendant de la règle « fidèle » sur les 72 gestionnaires non cités (§5) : la liste est signalée, pas balayée.
3. `0x01` : marqué NOOP_DELIBERATE alors que la DLL rend 1 et que le binaire rend 0 (`:617-618`). L'inventaire le signale, mais le classement reste « délibéré ».

**Fiabilité pour planifier des tranches :** utilisable pour dimensionner (sites sautés, cartes touchées, liste des 29 opcodes sautés). Insuffisante pour décider qu'un opcode est « fait » : chaque tranche doit reprendre une relecture au binaire, car « FAITHFUL » n'est qu'une présence.

**Fichiers écrits :** aucun (critique en lecture seule).