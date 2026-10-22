# Critique indépendante de la préparation E19.sm1 (0x60 0x61 0x3C 0x3D)

**Verdict global.** Le cœur technique de la préparation tient : règles R1 à R7, vecteurs, comptes, valeurs rouges d'aujourd'hui, insertion dans le runner. Je n'ai trouvé ni P0 ni P1. Deux erreurs P2 (§3) sont à corriger avant de soumettre sm1 à l'auteur. Plusieurs P3 et P4 sont listés au §3. Je n'ai rien pu mesurer à l'exécution : lecture seule, sans build, sans test ni ArcRun. Tout ce qui dépend de C# exécuté reste INCONNU.

Le dossier de sortie contenait déjà des fichiers d'un essai antérieur interrompu. Je les ai relus, rejoués et complétés. Tout est sous
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm1-critic/`
(ci-dessous `CRIT/`). Les sorties de chaque contrôle sont dans `CRIT/notes/out_*.txt`, les scripts dans `CRIT/work/`, les listings dans `CRIT/binary/`.

## 1. Ce que j'ai fait moi-même

- **Listing capstone des 4 gestionnaires** via la table `0x80098FAC`, fait avant de lire le leur (`CRIT/binary/my-handlers.txt`).
  - FAIT : identique au leur, 179 instructions, 0 écart.
  - FAIT : SHA-1 du binaire égal à `a705f8da…`.
- **Répartiteur désassemblé** (`CRIT/binary/my-dispatcher.txt`, `0x800422BC-0x800422F4`).
  - FAIT : a0 = mot `+0x230` du propriétaire (l'entité logique), a1 = propriétaire, a2 = `&pc`, a3 = état. a1 n'est lu par aucun des 4 gestionnaires.
- **Interpréteur MIPS écrit de zéro**, par champs de bits, sans capstone ni leur `emu.py` (`CRIT/work/mymips.py`).
  - FAIT : leurs 1 384 vecteurs rejoués avec et sans « junk » au-delà de N, 0 écart sur `Result` et sur la taille rendue.
  - FAIT : aucune écriture hors `Result` et hors tampon de recherche, aucune lecture au-dessus de N.
- **Régénération** : leur générateur copié dans `CRIT/regen/`.
  - FAIT : fichier de vecteurs identique à l'octet, SHA-256 `a8c599c9…2069`.
- **Mon propre modèle de la DLL** (`mymodel.py`, écrit depuis le C# et R1 à R5) :
  - FAIT : 1 384 sur 1 384 égaux au binaire en montage prod. Mes drapeaux « le héros hors de la liste change le résultat » sont les mêmes que leurs 14 vecteurs.
- **Mon propre générateur** : 80 000 mondes aléatoires (4 graines, 20 000 par opcode), binaire réel contre mon modèle.
  - FAIT : `0x3C` et `0x3D`, 0 écart en prod et en ctx.
  - FAIT : `0x60` et `0x61`, 0 écart en prod, sauf 3 mondes (0x60, groupe `0x8A`, proxies nus).
  - FAIT : en ctx, les écarts viennent des groupes `0x82` et `0x84` seulement (plus ces mêmes 3).
- **Recensement des sites** par un parcours de flot de contrôle écrit de zéro, partant de toute entrée non nulle des 6 tables de programmes, sans passer par les enregistrements.
  - FAIT : exactement les mêmes 187 (carte, pc) que `skipped-sites.tsv`, dans les deux sens. Les 5 programmes nommés dont l'offset de table vaut 0 (cartes 1, 43, 335) n'atteignent aucun des 4 opcodes.
- **53 scénarios d'octets réels** : mon mini-interpréteur, écrit depuis les `case` du runner (`CRIT/work/rb_expected.py`).
  - FAIT : les octets de chaque scénario sont égaux à ceux de l'export.
  - FAIT : les 53 états « rouge aujourd'hui » et les 53 états « attendu » (pc final, `Result`, drapeaux) sont retrouvés. Pour l'attendu, j'ai écrit l'oracle à partir des titres des scénarios (qui monte sur quoi).
- **Mutants** : j'ai recalculé les ensembles de morts depuis les définitions des tests, puis ajouté 9 mutants.

## 2. Verdicts sur les faits qui portent une décision

| # | Fait | Verdict | Pièce |
|---|---|---|---|
| D1 | `0x60` (taille 2) = au moins un membre du groupe a `+0x12C` égal à a0. Aucun test de statut, de `+0x20` ni de a1. | CONFIRMÉ | `0x8003F3F8`, `0x8003F444-0x8003F44C` |
| D2 | `0x61` (taille 2) = `+0x12C` de a0 égal à un membre. Un porteur nul donne toujours 0. | CONFIRMÉ | `0x8003F4D0-0x8003F4D8` |
| D3 | `0x3D` (taille 7) : emplacements 0 à N inclus, héros compris. Statuts 1, 2, 3 (donc pas `IsActive`). Bornes inclusives signées. Octets lus non signés. `Result` toujours écrit. | CONFIRMÉ | `0x8003E64C`, boucle `t0 <= N` |
| D4 | `0x3C` = `0x3D` plus Flags bit 0x80 présent, AnimFlags bit 0x80 absent, `+0x28` nul. C'est exactement `EntitySupport.IsEligibleSubject`, et l'inverse de la décompilation. | CONFIRMÉ | `Script_60_03C` : `NoEntityCollision != 0 && Collidable == 0` |
| D5 | Mapping octets vers membres de `read-map.tsv`. | CONFIRMÉ | voir note P4-a sur AnimFlags |
| D6 | Tuile non signée contre signée : mutant équivalent. | CONFIRMÉ | les bornes sont dans 0..255 : une tuile négative échoue dans les deux cas. `check_sm12` M10 est faux sur ce point. |
| D7 | 1 384 vecteurs, SHA-256 figé, 0 écart binaire, modèle prod et modèle DLL. | CONFIRMÉ | §1 |
| D8 | 187 sites, 11 cartes : 172 `0x60`, 14 `0x61`, 1 `0x3D`, 0 `0x3C`. Groupes : 178 identifiants bruts, 8 pour le héros `0x81`. Les 178 identifiants nomment un enregistrement `IsEnabled=1` de leur carte. | CONFIRMÉ | `CRIT/notes/out_rawids_enabled_records.txt` |
| D9 | Comptes après sm1 : 510 vers 323 lignes, 508 vers 321 instructions, 96 vers 89 cartes, 21 vers 18 opcodes, 7 cartes disparaissent. | CONFIRMÉ | par propriétaire : 129 + 42 + 90 + 17 + 42 + 3 = 323 |
| D10 | « Rouge aujourd'hui » : `UnknownOpcode` rend la taille, `Result` inchangé, `Kind = UnknownSkipped`. | CONFIRMÉ | `EventProgramRunner.cs:3065-3091` |
| D11 | Forme des sites : 144 + 17 + 4 + 6 + 2 + 2 + 1 + 3 + 1 + 7 = 187. Les 19 sites à écrivain voisin (le 3B de 159 B[5] est mêlé à l'entrée) sont décodés dans 27, 77, 398 et 226. | CONFIRMÉ (statique) | le comportement à l'exécution reste INFÉRENCE |
| D12 | Chez Lurvy (398), aujourd'hui, `43 0N` écrit 1, `61 00` saute, et `03 64 00` mène à `@191` : boucle de dialogue. | CONFIRMÉ (décodage) | `decode.py 398 80 290` ; à l'exécution : INFÉRENCE (AR-04) |
| D13 | Le héros est en `SpawnedEntities[0]` en production. Le faux `Context` de test ne l'y met pas. | CONFIRMÉ | `WorldProxy.cs` `InsertBySlot(entity, 0)` ; `AlundraClassAndBranchOpcodesTests.cs:25-35` |
| D14 | O14/I9 levé : une case recyclée sort de la liste et son `EntityRefId` passe à -1. | CONFIRMÉ | la valeur du modèle à l'exécution n'est pas relue au binaire |
| D15 | Grille statique des drapeaux : plateau `0x60C0`, objets `0x332384`. | CONFIRMÉ | recalculée depuis `sprite-records.json` : MoreFlags, CanPickup, FPST |
| D16 | Table des mutants : 36 tués, 1 équivalent. | PARTIELLEMENT RÉFUTÉ | 33 lignes exactes, 2 sous-estimées (inoffensif), 1 fausse : `owner_instead_of_logic` (§3, P2-b) |
| D17 | Preuve de retrait : les tests par vecteurs sont tous rouges (attendu 0 ou 1 contre 7). | CONFIRMÉ | 32 rouges et 21 gardes parmi les 53 scénarios RB, cohérents |
| D18 | Libellés R8 non épinglés hors annexe et triage. | CONFIRMÉ | recherche dans tout le dépôt |
| D19 | Miroirs R9 : `ImplementedOpcodes` à `IntroTraceHarnessTests.cs:332`, `IsPorted` seul, `PredicateOpcodes` inchangé. | CONFIRMÉ | aucun des 4 opcodes sur les 30 cartes de la chaîne |
| D20 | Arrivées et signaux de fin des arcs. | CONFIRMÉ | portail 1 de la 26 vers (13,57) ; 20 vers 398 en (7,31) ; 34 vers 400 en (9,17) z 5 ; signaux `(2,999,0x05)` 0x8104, `(2,578,0x05)` 0x8014, `(1,191,0x05)` |
| D21 | Chargement des cartes 27, 400 et 398 par ArcRun. | NON ÉTABLI | T0 |
| D22 | « Q-sm-5 est périmé » (§2 point 4). | RÉFUTÉ | §3, P2-a |
| D23 | M12 « résolu » (prémisse des interrupteurs au sol). | NON ÉTABLI | §3, P3-c |
| D24 | Prérequis : règle du chantier `plan-restructuration-dll.md:159-160` ; Q-E19-SM1 ouverte ; sm0 READY mais non exécutée ; R5 en attente de Q-R-3. | CONFIRMÉ | ADR libre suivant : 0076 |

## 3. Constats, sévérités et corrections

**P2-a. Q-sm-5 n'est pas périmé : les objets des plaques sont de la famille C4, hors de l'ensemble câblé d'E20.g.**
- FAIT : dans `sprite-records.json`, `ProgramTick = 4` pour exactement 107, 111, 112, 113, 117, 34 et 37. `ProgramTick = 23` pour 182, 35, 36, 38, 41, 48 et `alundra_234`.
- FAIT : `plan-e20-heros.md:6558` liste C4 = (107, 111, 112, 113, 117, 34, 37). Le D-G-8 (`:6590`) câble seulement « les 7 types C23 » et dit « Torches, C4, graines, bombe : hors ensemble ». O-E20-21 (`:9294`) : aucune tranche pour C4.
- FAIT : les objets « étoile, lune, eau, soleil (transportable) » des cartes 27, 29, 30 et 400 sont les sprites 107, 111, 112 et 113. Les tonneaux de la 113 sont le sprite 34. Seules les cruches de la 159 (sprite 35) sont C23, donc câblées.
- Conséquence : 167 des 187 sites (cartes 27, 29, 30, 113, 400) restent insolubles même après E20.g1b. `check_sm12` O2 (« en partie périmé ») se fonde sur le seul `EntityName`, et son I5 gardait déjà « g2c ou C4 ».
- Le coût d'« Option A » de Q-1 est donc plus lourd que l'énoncé. Le passage de la carte 27 (27 C[36], 4 sites), ouvert aujourd'hui par accident, restera fermé tant qu'aucune tranche ne porte les objets C4, et aucune n'est prévue. Ce n'est pas « jusqu'au transport ».
- **Correction** : réécrire §2 point 4 et Q-1. Q-sm-5 reste ouverte. Poser à l'auteur : (A) sm1 d'abord et accepter ce blocage sans remède planifié, ou (B) une tranche C4 d'abord. Retirer de la recette toute attente de plaques résolues.

**P2-b. La ligne `owner_instead_of_logic` de `mutants.tsv` et de `real-bytes-kills.tsv` est fausse, et R6 n'a aucun test unitaire pour `0x61`.**
- FAIT (README des vecteurs) : le propriétaire est l'entité logique dans tous les tests par vecteurs, sauf ceux nommés « owner = hero ».
- Le mutant ne peut donc être tué que par RP-07 (0x60). Les 13 autres tests cités (RP-01, -02, -04, … -30) ne peuvent pas le tuer. Je l'ai recalculé : seul RP-07 le tue, y compris avec les 1 384 vecteurs rejoués.
- Côté octets réels, seuls RB-03 (le `43` retargete) et RB-12 le tuent, car RB-01 à -08, -10, -13 et -14 sont des programmes C où propriétaire et entité logique coïncident. Les lignes RB-01 etc. du fichier sont donc fausses.
- **Correction** :
  - corriger les deux lignes ;
  - ajouter RP-28 : 0x61, propriétaire = héros, `LogicEntity` = l'objet, attendu 1 puis 0 (la même paire que RP-07) ;
  - dire que R6 de `0x61` n'est tenu que par RB-12 (octets réels) et RP-28.

**P3-a. Survivants.**
- FAIT : `hero_status_bypass` (visiter `PlayerEntity` sans le filtre de statut) survit à tous les tests nommés et aux 1 200 vecteurs aléatoires.
- `logic_not_blocked` et `logic_loaded_normal_deact` (0x60/0x61 exigeant que l'entité logique soit vivante ou non bloquée) ne sont tués que par les vecteurs aléatoires, pas par un test nommé.
- **Correction** :
  - ajouter ZP-3D/3C : le héros seul dans la boîte avec statut 0 puis 4, attendu 0 ;
  - ajouter RP : entité logique de statut 0 ou 4, groupe `0x80` ou `0x81`, attendu le résultat binaire ;
  - ajouter RP : porteur ou membre bloqué en 0x61.

**P3-b. Réglages d'arcs pour 27 et 400.**
- FAIT : `ArcSpec("SM1-27", …, 60)` n'a ni `RealController` ni `Prefabs` (`AlundraArcSupport.cs:52-64`) : les PNJ sont alors nus. Or la pose au sommet et la prémisse `Assert.Same(plaque.LogicContextEntity, objet.RidingEntity)` exigent les boîtes et le drapeau Gravity de l'en-tête (`FindRidingEntity`).
- `Prefabs` exige `RealController` (`ArgumentException`, `:185-188`). Un arc `RealController` doit finir à 0 glissement (`:342-356`).
- **Correction** : écrire `RealController: true, Prefabs: true` pour AR-01 et AR-03, comme pour AR-04, et laisser T0 sonder les deux modes.

**P3-c. Les interrupteurs au sol de la 136 ne sont pas vérifiés (M12 « résolu » est abusif).**
- RB-08 pose `RidingEntity` à la main. Aucun test ne passe par `UpdateRidingEntityOfHero` avec l'interrupteur réel (sprite 45, MoreFlags 128, donc Collidable, hauteur 16). `check_sm12` O12 reste ouvert.
- **Correction** : ajouter CH-04 (le héros posé sur le sprite 45, puis `UpdateRidingEntityOfHero`), ou marquer M12 non résolu.

**P4.**
- **a.** Le « drapeau » des AnimSets cité est le champ `Flags` (0 ou 4). L'`AnimFlags` de la DLL vient de `AnimSetEntry.Acceleration` (`EntitySpawnFactory.cs:553`, `FrameSyncPasses.cs:158`) : 0, ou 64 pour le plateau 108, jamais le bit 0x80. Conclusion inchangée.
- **b.** Latent dans `EntitySearchService` (types 5, 6, 9, 10) : `ReferenceEquals(null, null)` fait correspondre un proxy nu sans `LogicContextEntity`. E12.d n'a corrigé que les types 7 et 8. Aucun vecteur gelé ni site du corpus ne le déclenche. Interdire `anon` avec les groupes `0x85`, `0x86`, `0x89` et `0x8A` dans les vecteurs.
- **c.** Les arcs sur 29, 136, 159 et 398 croisent encore des opcodes sautés (sm4, sm7 : par exemple 3 sites `B9` dans la 398). Leurs assertions ne doivent pas exiger un `SkippedOrExceeded` vide.
- **d.** `SpawnedEntities` de `WorldProxy` fabrique une liste neuve à chaque lecture (`GetSpawnedEntityProxies`), et le service de recherche en alloue une autre. C'est négligeable ici, mais « boucle indexée » ne supprime pas l'allocation de la lecture.

## 4. Verdict par tâche proposée

- **T0** : CONFIRMÉE, avec trois ajouts. Mesurer en `RealController + Prefabs` pour 27 et 400 (P3-b). Ajouter la sonde du héros sur l'interrupteur 136 (P3-c). Consigner le fait C4 (P2-a).
- **T1** : CONFIRMÉE sous réserve des corrections P2-b et P3-a. Chaque valeur est rouge à 7 puis 0/1. Les 21 lignes RB gardes sont vraies, donc vertes d'avance.
- **T2** : CONFIRMÉE, un seul commit pour le code, les libellés, le miroir et la liste. Les fichiers touchés sont exactement ceux annoncés ; `using Alundra.Physics;` est déjà présent, donc aucun type neuf. Le point d'insertion existe : `case 0x42` est à la ligne 1987.
- **T3** : CONDITIONNELLE, sur P3-b et sur D21.
- **T4** : CONFIRMÉE. La recette doit retirer les plaques résolues (P2-a). Chez Lurvy, la boucle de dialogue s'arrête mais aucun achat n'est possible : les objets de la boutique sont des sprites globaux à `ProgramTick 255`, donc non soulevables.
- **Q-2** (porter `0x3C`) : acceptable, c'est le périmètre du plan. **Q-3** (attente infinie en 226) : INFÉRENCE plausible, à garder. **Q-6** (pas d'ADR, prochain numéro 0076) : acceptable.

## 5. Cas manquants

À ajouter, au-delà de P3-a et P3-c :
- Un vecteur 0x61 avec propriétaire différent de l'entité logique (RP-28).
- Les statuts du héros dans la boîte (ZP-3C et ZP-3D).
- Une entité logique non vivante avec un groupe qui n'est pas un identifiant brut.
- Un porteur bloqué en 0x61.
- Le héros sur l'interrupteur 136 par `UpdateRidingEntityOfHero` (CH-04).

## 6. Fichiers produits (`CRIT/`)

- `binary/my-handlers.txt` : listing des 4 gestionnaires. `binary/my-dispatcher.txt` : répartiteur. `binary/my-search.txt` : recherche `0x8003C954`.
- `work/mymips.py` : interpréteur MIPS. `work/mymodel.py` : modèle de la DLL. `work/replay_binary.py` : rejeu de leurs vecteurs. `work/gen_compare.py` et `work/classify_diffs.py` : mon générateur et le classement des écarts.
- `work/mutants_eval4.py` : mutants, avec les 9 mutants ajoutés et le rejeu complet des vecteurs.
- `work/rb_expected.py` : 53 scénarios d'octets réels. `work/cfg_nonzero2.py` : recensement des sites. `work/check_rawids.py`, `work/records_of_sites.py`, `work/portal_reach.py`, `work/decode.py`, `work/map_records.py`.
- `regen/` : copie du générateur et fichier régénéré, identique à l'octet.
- `notes/out_*.txt` : une sortie par contrôle.