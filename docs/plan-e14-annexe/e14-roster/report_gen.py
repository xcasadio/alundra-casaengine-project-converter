"""Generates report.md (French) from the computed data. Read-only on the repository. Run after build_outputs.py and slices.py:
python -B report_gen.py"""
import collections
import csv
import json
import os
import pickle
import sys

import common
import decomp_index

sys.stdout.reconfigure(encoding="utf-8")
HERE = common.HERE
LETTERS = "ABCDEF"
DIAMOND = "\u25c6"
d = pickle.load(open(os.path.join(HERE, "cache", "report_data.pkl"), "rb"))
c = pickle.load(open(os.path.join(HERE, "cache", "census.pkl"), "rb"))
slices = json.load(open(os.path.join(HERE, "cache", "slices.json"), encoding="utf-8"))
names = common.load_entity_names()
cat = common.load_maps_catalog()
impl = common.parse_dll_implementation()
roster_rows = list(csv.DictReader(open(os.path.join(HERE, "roster.tsv"), encoding="utf-8"), delimiter="\t"))
handler_rows = list(csv.DictReader(open(os.path.join(HERE, "handlers.tsv"), encoding="utf-8"), delimiter="\t"))
HR = {r["key"]: r for r in handler_rows}
R = {r["type"]: r for r in d["roster"]}
H = {h["key"]: h for h in d["handlers"]}
T = c["types"]
data = common.load_cache()
maps = data["maps"]
ENEMYISH = ("enemy", "boss", "enemy (spawned only, no map record)")


def nm(t):
    return names[t][0].lstrip(DIAMOND) if t < len(names) else str(t)


def mp(m):
    return f"{m} {cat.get(m, ('?', '?'))[1]}"


def fmt(n):
    return f"{n:,}".replace(",", " ")


def table(header, rows):
    out = ["| " + " | ".join(header) + " |", "|" + "|".join("---" for _ in header) + "|"]
    for r in rows:
        out.append("| " + " | ".join(str(x).replace("|", "/") for x in r) + " |")
    return "\n".join(out)


# ------------------------------------------------------------------------------------------------ numbers
n_maps = len(maps)
n_rec_all = sum(len(m["entities"]) for m in maps.values())
n_rec_en = sum(1 for m in maps.values() for e in m["entities"] if e["IsEnabled"])
n_headers = len(roster_rows)
n_hdr_g = sum(1 for r in roster_rows if r["page"] == "G")
n_hdr_m = sum(1 for r in roster_rows if r["page"] == "M")
n_seen = sum(1 for r in roster_rows if int(r["records_enabled"]) > 0)
exe_entries = [h for h in handler_rows if h["exe_table_entry"].startswith("0x")]
per_slot = collections.Counter(h["slot"] for h in exe_entries)
n_addr = len({h["exe_table_entry"] for h in exe_entries})

cls = collections.OrderedDict()
for r in d["roster"]:
    cls.setdefault(r["role"], [0, 0, 0])
    cls[r["role"]][0] += 1
    if r["recs"]:
        cls[r["role"]][1] += 1
        cls[r["role"]][2] += r["recs"]

key_inst = collections.defaultdict(lambda: [0, 0])
tot_inst = [0, 0]
for t, x in T.items():
    for (s, cc), n in x["native_keys"].items():
        if cc == 0 and s in (2, 3, 5):
            continue
        k = f"{LETTERS[s]}{cc}"
        ok = k in H and t in H[k]["implTypes"]
        key_inst[R[t]["role"]][0] += n
        key_inst[R[t]["role"]][1] += n if ok else 0
        tot_inst[0] += n
        tot_inst[1] += n if ok else 0
en_inst = key_inst["enemy"][0] + key_inst["boss"][0]
en_ok = key_inst["enemy"][1] + key_inst["boss"][1]

enemy_rec = [t for t, r in R.items() if r["role"] in ("enemy", "boss")]
enemy_none = [t for t in enemy_rec if R[t]["status"].startswith("none")]
enemy_partial = [t for t in enemy_rec if R[t]["status"] == "partial"]
enemy_done = [t for t in enemy_rec if R[t]["status"].startswith("implemented")]
bosses = [t for t in enemy_rec if R[t]["role"] == "boss"]
spawned_only = [t for t, r in R.items() if r["role"] == "enemy (spawned only, no map record)"]
n_enemy_recs = sum(T[t]["recs"] for t in enemy_rec)
maps_with_enemy = {m for t in enemy_rec for m in T[t]["maps"]}
maps_with_none = {m for t in enemy_none for m in T[t]["maps"]}
recs_none = sum(T[t]["recs"] for t in enemy_none)
ld = sum(T[t]["load_spawn"] for t in enemy_rec)
so = sum(T[t]["script_spawn"] for t in enemy_rec)
c_nat = sum(T[t]["slot_native"].get(2, 0) for t in enemy_rec)
c_scr = sum(T[t]["slot_scripted"].get(2, 0) for t in enemy_rec)
ho_c = sum(n for t in enemy_rec for (a, b, v), n in T[t]["handover"].items() if b == 2 and (v & 0x7F) == 0)
n_hostile_proj = cls["projectile/effect (hostile)"][0]
n_attached = sum(v[0] for k, v in cls.items() if k.startswith("enemy-attached"))
hz = [(k, v) for k, v in cls.items() if k.startswith("hazard")]
n_hz_types = sum(v[0] for _, v in hz)
n_hz_recs = sum(v[2] for _, v in hz)
a0_recs = c["keys"][(0, 0)]["recs"]
n_diamond = sum(1 for r in roster_rows if r["name_fr"].startswith(DIAMOND))

guard_total = guard_ok = 0
for r in handler_rows:
    if r["decomp_guard_names(entity.Name checks)"]:
        guard_total += 1
        if not r["guard_names_not_in_census"] and not r["census_types_not_in_guard"]:
            guard_ok += 1

all_sites = decomp_index.build_spawn_sites()
n_sites = len(all_sites)
n_dyn = len(d["dynamic_sites"])

per_map = collections.defaultdict(collections.Counter)
for t in enemy_rec:
    for m, n in T[t]["maps"].items():
        per_map[m][t] += n

users_count = collections.Counter()
for t, r in R.items():
    if r["role"] in ENEMYISH:
        for k in r["missing"]:
            users_count[k] += 1

# ------------------------------------------------------------------------------------------------ text
L = []
w = L.append

w("# enemies2 : roster complet de l'IA native des sprites (Alundra vers CasaEngine)")
w("")
w("Lecture seule du dépôt, état du 2026-10-17 : branche `chantier/script-types-full-name`, HEAD `7d776b3` (DLL `Alundra/Scripts/`, annexes `docs/`). Tout vient de scripts du dossier de travail (section 9). Étiquettes : **FAIT** (vu dans le code ou les données, avec sa source), **INFÉRENCE** (déduit, la déduction est écrite), **INCONNU** (non établi).")
w("")
w("## 0. Résultat")
w("")
w(f"- **FAIT** : {n_maps} cartes, {fmt(n_rec_all)} enregistrements `Entities`, dont {fmt(n_rec_en)} activés (`IsEnabled`). {n_headers} types de sprite ont un en-tête ({n_hdr_g} page globale, {n_hdr_m} page carte) ; {n_seen} sont dans au moins un enregistrement activé.")
w(f"- **FAIT** : le binaire a {len(exe_entries)} entrées non nulles dans les tables de gestionnaires natifs ({n_addr} adresses distinctes) : " + ", ".join(f"{k} {v}" for k, v in sorted(per_slot.items())) + f". Elles correspondent une à une à `SpriteEventHandlers.cs` ; les cinq `AI_EmptyFunction` de la décompilation (A13, C0, D0, D4, F0) sont des mots nuls dans le binaire.")
w(f"- **FAIT** : {fmt(tot_inst[0])} « instances » de clé native (enregistrement x emplacement natif, sans les entrées nulles C0, D0, F0) ; la DLL en couvre {fmt(tot_inst[1])} ({100 * tot_inst[1] // tot_inst[0]} %), surtout E0/E1 en ligne. Pour les ennemis et les boss : {fmt(en_ok)} sur {fmt(en_inst)}.")
w(f"- **Ennemis** (classe `enemy` ou `boss`, définition section 3) : **{len(enemy_rec)} types avec enregistrements** ({fmt(n_enemy_recs)} enregistrements sur {len(maps_with_enemy)} cartes) dont {len(bosses)} boss, et **{len(spawned_only)} types ennemis sans enregistrement** (nés d'un `SpawnWarpEntity`). À côté : {n_hostile_proj} projectiles hostiles, {n_attached} objets rattachés à un ennemi, {n_hz_types} dangers non ennemis ({fmt(n_hz_recs)} enregistrements).")
w(f"- **Portés aujourd'hui** : 2 types ennemis, Beannoïde (335, partiel) et Muruta (347, complet pour ses clés d'en-tête). **{len(enemy_none)} types avec enregistrements n'ont aucune clé native portée** ({fmt(recs_none)} enregistrements sur {len(maps_with_none)} cartes), boss compris ({len([t for t in enemy_none if R[t]['role'] == 'boss'])}). Le résultat est très au-dessus des quatre types non portés d'une passe antérieure, qui ne comptait que les utilisateurs de A1, D1, C1, C10 et D9.")
w("- **Côté histoire** (seul ordre connu : les 37 cartes de `docs/plan-e19-opcodes.md`) : les types non portés qu'on y rencontre sont Poisson et Guêpe (carte 14, jour 2), Orc (masse) (carte 15, jour 2) et Slime géant grand/petit (carte 44, jour 3). Tous les autres apparaissent hors de la chaîne connue : l'ordre est **INCONNU** pour eux.")
w("- **Trouvailles qui changent le plan** : (a) E6, E7, E8, E10 et E27 ont un corps identique octet pour octet à E0/E1 (`DestroyEntity(e, -1)`), alors que la DLL ne traite en ligne que 0, 1 et 10 ; (b) A254, C24, C57 et D25 sont des fonctions vides (`jr ra`) : rien à porter ; (c) dix codes D ont exactement la forme de `HitReaction` (D1, D6, D7, D8, D9, D11, D13, D17, D19, D24 : 25 mots, seule la constante d'animation change) ; (d) le registre refuse une seconde inscription d'une même clé, alors que D6 et D11 servent chacun 8 types ; (e) cinq aides du binaire manquent à la DLL et `CreateEffectEntity` n'est porté que pour l'effet de bris (section 6) ; (f) E11 n'est pas un alias de E1 : c'est la destruction récursive des enfants d'un boss ; (g) la décompilation ajoute des gardes de nom (`entity.Name != ...`) qui donnent, par gestionnaire, les noms de sprite attendus : elles recoupent le recensement dans " + f"{guard_ok} cas sur {guard_total}.")
w("")

w("## 1. La règle de répartition, ré-établie")
w("")
w("**FAIT, binaire** (`ALUN_CD.EXE` France, `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE`, désassemblé avec capstone, `mipsdis.py`) :")
w("")
w("1. `0x800388F8-0x80038944` (passe des événements) : pour l'emplacement en attente `s` (`entity+0x228`), si `entity[+0x4C+4*s] & 0x7F == 0` la fonction `table[s][ entity[+0x70+4*s] ]` est appelée, **sauf si le mot de la table est nul** (`beqz` en `0x80038934` : aucun appel) ; sinon le programme d'octets `0x8004205C` tourne. Les tables sont atteintes par le tableau maître `0x80098F4C` = {A `0x800C4B34`, B 0, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`, F `0x800C5B34`}, 256 mots chacune.")
w("2. `InitializeCodePrograms` `0x8004201C` : si l'entité a un enregistrement (`entity+0x44`), `entity[+0x4C+4*i] = (octet signé) enregistrement[0x0A+i]` pour i = 0..5 (A, B, C, D, E, F). Une entité née sans enregistrement (`SpawnWarpEntity`) garde 0 : tous ses emplacements sont natifs.")
w("3. `InitializeEntity` `0x80039DD4-0x80039E10` : les octets d'en-tête `+0x13..+0x17` (ProgramLoad, Tick, Touch, Deactivate, Interact) sont copiés en `entity+0x70` (A), `+0x78` (C), `+0x7C` (D), `+0x80` (E), `+0x84` (F) ; `+0x74` (B) vaut 0.")
w("")
w("Donc : **un emplacement tourne en natif quand l'index de l'enregistrement, masqué par `0x7F`, est 0 ; le gestionnaire est celui que nomme l'en-tête du sprite.** Le bit `0x80` de l'index choisit la table de la carte ; clair avec un index non nul, c'est la table globale : 6 enregistrements seulement, tous du type 409 (Pot magique), emplacement C. C'est la règle de `dll-natives.md` §0 point 3, confirmée dans le binaire.")
w("")
w("**FAIT, données** : `census.py` (enregistrements activés de `data-extracted/data/map_N.json`, 483 cartes) reproduit à l'unité `docs/plan-e14-annexe/e14a/dll-natives.md` §0.3 : 9 631 enregistrements activés ; Beannoïde 130 enregistrements sur 18 cartes ; Muruta 203 sur 25 ; A1 : 335 x120, 329 x118, 401 x84, 410 x76, 395 x4 ; D1 : 335 x130, 329 x118 ; C1 : 335 x105 ; C10 : 347 x28 ; D9 : 347 x198. Les sept champs d'en-tête lus (cinq codes, `BreakEffect`, `Contents`) sont identiques d'une carte à l'autre et à `alundra-project/Data/sprite-records.json` (0 écart sur les 244 types de la page carte et les 151 de la page globale).")
w("")
n_nz = len([k for k in c['keys'] if k[1] > 0 and c['keys'][k]['recs'] > 0])
w(f"**FAIT (réconcilié)** : l'annexe compte « 121 couples (emplacement, code) distincts, code non nul ou emplacement A » ; ce recensement trouve {n_nz} couples à code non nul atteints par au moins un enregistrement, plus A0 : {n_nz + 1}.")
w("")
w("**Type de sprite** : `SpriteTableIndex`, plus `0x100` quand `SpriteDirection & 0x80` (page carte) : `GameEngine.SpawnEntity` (décompilation) et `EntityRecordMapper.ResolveSpriteType` (`Alundra/Scripts/EntityRecordMapper.cs:226`). Un enregistrement n'existe que si `IsEnabled != 0` (`GameEngine.GetEntityRecord`) ; il naît à la charge de la carte seulement si `SpriteDirection & 0x40`, sinon par script (`0x2D`, `0x8A`, `0x8B`).")
w(f"Parmi les enregistrements ennemis et boss : {ld} naissent à la charge, {so} seulement par script. L'emplacement C est natif d'emblée pour {c_nat} enregistrements et scripté pour {c_scr} ; {ho_c} occurrences de `0x40 [C, 0]` dans leurs programmes scriptés le rendent natif ensuite (le hall des Muruta de la 115 est le cas connu).")
w("")

w("## 2. Ce que la DLL exécute aujourd'hui")
w("")
reg_rows = []
for (s, code), types in sorted(impl["registry"].items()):
    reg_rows.append((f"{LETTERS[s]}{code}", ", ".join(f"{t} {nm(t)}" for t in sorted(types)), impl["sources"][(s, code)]))
w(table(["clé", "types", "inscription (registre de production)"], reg_rows))
w("")
w(f"- **FAIT** : E0, E1 et E10 sont traités en ligne **pour tout type** (`AlundraEventProgramRunner.IsInlineDeactivateCode`, `AlundraEventProgramRunner.cs:{impl['inline_line']}`) : `DestroyEntity(e, -1)` avec l'effet de bris de l'en-tête, sans butin (le butin au décès est E20.e0c, `docs/plan-e20-heros.md:605-632`).")
w("- **FAIT** : le livre de sauvegarde (type 237) : F1 et C72 (`AlundraSaveBook.cs`).")
w("- **FAIT** : toute autre entrée est un no-op compté (`RunSpriteEvent`, `AlundraEventProgramRunner.cs:314-374`), ce que fait aussi le binaire pour un mot nul et pour les quatre fonctions vides.")
w("- **FAIT** : `AlundraNativeSpriteRegistry.Register` **lève une exception** si la clé (emplacement, code) est déjà inscrite (`AlundraNativeSpriteRegistry.cs:47-60`). Or les codes sont partagés par construction (section 5.2) : toute tranche qui ajoute un type à A1, D1, C1, C10 ou D9, ou qui inscrit D6 ou D11 pour un deuxième type, bute dessus. Il faut une inscription cumulative des types (prérequis de S0).")
w("- **FAIT** : le Beannoïde n'est que partiel. Dix de ses enregistrements (cartes 472 et 473) exécutent dans un programme A scripté `0x41 [C,99]` (x9), `0x41 [D,44]` (x10) et `0x41 [C,96]` (x1), qui remplacent le code du sprite par C99, D44 et C96 ; ces trois clés ne sont pas inscrites. **INFÉRENCE** : elles ne tournent que si l'emplacement est natif à ce moment-là. Hors chaîne de l'histoire.")
w(f"- **FAIT** : A0 n'est inscrit que pour le type 347 ; les {fmt(a0_recs)} enregistrements dont A est natif avec le code 0 ne l'exécutent pas (invisible tant que rien ne lit `AIValues[0]`, décision AD-3 de `dll-natives.md`).")
w("")

w("## 3. Classification, preuves")
w("")
w("Aucune classe n'est posée sur un nom seul. Signaux, par ordre de force :")
w("")
w("1. **FAIT, en-tête + binaire** : `MoreFlags` (octet d'en-tête `0x10`) porte les classes de combat (`Gameplay/EntityFlags.cs` de la décompilation, bits 0 à 3 : ClassA = frappé par le héros, HitsClassB = blesse le héros ; le héros a `0x8C`). Un type est `enemy` quand il est frappable (ClassA) **et** que son gestionnaire D natif appelle `ComputeNewHp` `0x8003A374` (vérifié par les appels `jal` de l'étendue du gestionnaire dans le binaire). Il est `boss` s'il a en plus une preuve de boss (point 5).")
w("2. **FAIT, binaire** : projectile hostile = blesse le héros sans être frappable (HitsClassB seul), avec bits de désactivation ou nom de projectile ; arme du héros = types 1 à 24 et 27 à 29 de la page globale (noms).")
w("3. **FAIT, en-tête** : portrait de dialogue présent (`FlagsPortraitShadowType & 0x80`) = PNJ qui parle.")
w("4. **FAIT, en-tête** : objet ramassable = en-tête A255/C255 (C255 est le gestionnaire de l'objet ramassable, `docs/plan-e20-heros.md:547`).")
w("5. **Boss** : *FAIT de placement* si un code de l'en-tête est enregistré dans `Gameplay/Scripts/Boss/` de la décompilation (A22, C40, C41, C77, C88, C90). *INFÉRENCE* sinon : tous les enregistrements dans une carte nommée `(Boss)` ou `Arena`, ou au plus 5 enregistrements dont un dans une telle carte, ou nom contenant `Boss`, ou nom de fonction C de la décompilation contenant Boss, Twin, Watcher, Melzas ou Special.")
w(f"6. **INFÉRENCE (recoupement, pas un critère)** : le marqueur `{DIAMOND}` du nom (`EntityNames.csv`, submodule de l'analyseur, colonnes japonaise et française, {n_diamond} lignes). Les {n_diamond} types `{DIAMOND}` tombent tous dans enemy, boss, projectile hostile ou objet rattaché ; aucun dans PNJ, objet ou objet ramassable. Hors `{DIAMOND}`, seuls les types 150 à 153 (projectiles génériques de la page globale) sont classés projectile hostile.")
w("")
LABEL = {
    "object/prop": "objet ou décor", "item (pickup)": "objet ramassable (item)", "NPC": "PNJ", "enemy": "ennemi", "boss": "boss",
    "projectile/effect (hostile)": "projectile ou effet hostile", "projectile/effect (hero weapon)": "arme ou magie du héros",
    "hazard/object (hurts the hero)": "danger qui blesse le héros, non frappable",
    "hazard/object (hittable and hurts the hero, no native hit handler)": "danger frappable et blessant, sans D natif qui appelle ComputeNewHp",
    "enemy-attached object (diamond name; hittable and hurts, no native hit handler)": "objet rattaché à un ennemi (nom diamant, frappable et blessant, sans D natif)",
    "enemy-attached object (diamond name, no combat bits)": "objet rattaché à un ennemi (nom diamant, sans bits de combat)",
    "enemy (spawned only, no map record)": "ennemi né à l'exécution (aucun enregistrement)", "unused slot": "emplacement inutilisé", "hero": "héros",
    "object (hero item: beans/bomb)": "objet du héros (haricots, bombe)",
}
rows = []
for k, v in sorted(cls.items(), key=lambda kv: -kv[1][2]):
    rows.append((f"`{k}`", LABEL.get(k, k), v[0], v[1], v[2]))
w(table(["identifiant (roster.tsv)", "libellé", "types", "types avec enregistrement", "enregistrements"], rows))
w("")
w("Les noms viennent de `alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv` (ligne i = type i ; 0-255 page globale, 256-511 page carte), la table que charge aussi le moteur d'origine selon `EntityNameCatalogReader.cs`. Les noms de carte viennent de `alundra-casaengine-project-converter/maps.json` (catalogue écrit à la main). Les gardes de nom de la décompilation (« Homme-lézard », « Zazan-Roi Muruta », « Tortue de roche », « Homme momie », « Homme de bois », « Homme-ombre », « Homme de boue », « Slime géant » dans `FunctionTypeD.cs`) sont analysées en 5.4.")
w("")

w("## 4. Les ennemis")
w("")
w("### 4.1 Cartes de la chaîne de l'histoire")
w("")
w("Source de l'ordre : `docs/plan-e19-opcodes.md:382` (chaîne 389 à 163), `:2564-2566` (jours 1 à 4), `:4118` et `:4153` (cartes de combat 14, 15, 44, 115-117, 362). L'ordre à l'intérieur d'une phase n'est pas donné par les plans ; les cartes 177, 180, 181, 182, 184 sont dans la chaîne sans jour précisé. `*` = type déjà porté (Beannoïde partiel).")
w("")
rows = []
for label, lst, src in d["STORY"]:
    for m in lst:
        items = per_map.get(m)
        if not items:
            continue
        rows.append((label, mp(m), ", ".join(f"{nm(t)} x{n}{'*' if R[t]['status'].startswith(('implemented', 'partial')) else ''}" for t, n in items.most_common())))
w(table(["phase", "carte", "ennemis (enregistrements)"], rows))
w("")
w("Les autres cartes de la chaîne (389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 169 à 175, 10, 179, 176, 135, 178, 183, 185, 177, 180 à 182, 184) n'ont aucun enregistrement ennemi activé. **FAIT** : les types portés y sont Beannoïde (14, 44, 116, 117) et Muruta (15, 115, 362). **INFÉRENCE** : les cinq premiers types non portés que le joueur rencontre sont Poisson, Guêpe, Orc (masse), Slime géant grand et petit ; la suite, au-delà du jour 4, est **INCONNUE**.")
w("")

w("### 4.2 Classement des ennemis non portés avec enregistrements")
w("")
w("`1re carte histoire` = première carte de la chaîne ci-dessus où le type apparaît, `-` sinon ; `prof.` = profondeur minimale en sauts depuis la carte 389 par les portails et les `0x53` (**INFÉRENCE** : indice, pas l'ordre de l'histoire ; les villes-carrefours aplatissent tout). `mots` = étendue majorée des clés manquantes dans le binaire (hors A0). `(n)` après une clé = nombre de types ennemis ou boss qui la réclament.")
w("")


def rank_row(t):
    r = R[t]
    keys = [k for k in r["missing"] if k != "A0"]
    words = sum(H[k]["words"] for k in keys if k in H)
    shared = [f"{k}({users_count[k]})" for k in keys if users_count[k] >= 2]
    first = f"{r['first_story']} [{r['story_phase']}]" if r["first_story"] is not None else "-"
    mps = sorted(r["mapIds"])[:3]
    return (nm(t) + (" (boss)" if r["role"] == "boss" else ""), t, r["recs"], f"{r['maps']}" + (f" (dont {r['devMaps']} test/inutilisées)" if r["devMaps"] else ""), first, r["depth"] if r["depth"] is not None else "-",
            ", ".join(mp(m) for m in mps), " ".join(r["missing"]), words, " ".join(shared))


hdr42 = ["type", "id", "enreg.", "cartes", "1re carte histoire", "prof.", "3 premières cartes (id)", "clés manquantes", "mots", "clés partagées"]
w("**Par nombre de cartes** (puis enregistrements) :")
w("")
w(table(hdr42, [rank_row(t) for t in sorted(enemy_none, key=lambda t: (-R[t]["maps"], -R[t]["recs"], t))]))
w("")
w("**Par première carte de l'histoire** (rang de phase, puis profondeur ; les types hors chaîne sont classés par profondeur, **INFÉRENCE**) :")
w("")


def story_key(t):
    r = R[t]
    sr = tuple(r["story_rank"]) if r["story_rank"] else (99, 0)
    return (sr, r["depth"] if r["depth"] is not None else 99, -r["maps"])


w(table(hdr42, [rank_row(t) for t in sorted(enemy_none, key=story_key)[:20]]))
w("")
w("Le Beannoïde (335) manque encore C96, C99 et D44 (section 2) ; il n'est pas dans ces tables.")
w("")

w("### 4.3 Types ennemis sans enregistrement (nés d'un `SpawnWarpEntity`)")
w("")
w(f"**FAIT (décompilation)** : `FunctionType{{A,C,E}}.cs` et `Boss/*.cs` appellent `SpawnWarpEntity` ou `SpawnChildEntity` avec un indice de sprite littéral dans {n_sites - n_dyn} sites sur {n_sites} ; les {n_dyn} autres sont dynamiques (objets lâchés `id + 0x1E`, cas génériques). Les enfants n'ont aucun enregistrement : ils n'existent qu'à l'exécution, tous leurs emplacements sont natifs, donc tous les codes de leur en-tête comptent.")
w("")
parents = collections.defaultdict(lambda: collections.defaultdict(set))
for h in d["handlers"]:
    fnn = h["func"].split(".")[-1]
    sp = d["spawn_types_by_func"].get(fnn)
    if sp and h["slot"] in (0, 2, 4):
        for ty in h["hdrTypes"]:
            for ch in sp:
                if ch != ty and ch in R:
                    parents[ty][h["key"]].add(ch)
rows = []
for ty in sorted(parents):
    if R[ty]["role"] not in ENEMYISH + ("projectile/effect (hostile)",) and not R[ty]["role"].startswith("enemy-attached"):
        continue
    kids = sorted({ch for v in parents[ty].values() for ch in v})
    rows.append((f"{ty} {nm(ty)}", ", ".join(sorted(parents[ty])), ", ".join(f"{ch} {nm(ch)} [{R[ch]['role'].split(' ')[0].split('/')[0]}]" for ch in kids)))
w(table(["parent", "gestionnaire(s) qui crée(nt)", "enfants (id nom [classe])"], rows))
w("")
w("Sites dynamiques (pour mémoire) : " + "; ".join(f"`{s['func']}` ({s['file'].split('/')[-1]}:{s['line']})" for s in d["dynamic_sites"]) + ".")
w("")
w("Les projectiles 150 à 153 de la page globale (génériques, C24 ou C36 + E3) n'ont pas de parent littéral. Un enfant sans enregistrement porté seul serait inatteignable : le parent d'abord.")
w("")

w("### 4.4 Projectiles hostiles, objets rattachés, dangers")
w("")
rows = []
for r in d["roster"]:
    if r["role"] == "projectile/effect (hostile)":
        rows.append((r["type"], nm(r["type"]), "C%d E%d" % (r["hdr"][1], r["hdr"][3]), r["recs"], " ".join(k for k in r["missing"] if k != "A0") or "aucune"))
w(table(["type", "nom", "C E (en-tête)", "enreg.", "clés manquantes dans la DLL"], rows))
w("")
w("**FAIT (binaire)** : C24 est la fonction vide (`jr ra`) ; la mort à l'impact est E3 (25 mots, `0x8007EDA0`) ; C36 (`0x8006E83C`, 24 mots) est le boomerang. **INFÉRENCE** : le mouvement vient du mouvement commun (vitesse de l'animation) et les dégâts au héros de la passe de coups par drapeaux, donc d'E20.c2b, pas de ces gestionnaires.")
w("")
w(f"Dangers non ennemis (classes `hazard/object`, {n_hz_types} types, {fmt(n_hz_recs)} enregistrements) : Rocher tombant (252), flammes (319, 343, 337), Sol piquant (318), boules de fer (310, 311), Torche (393), Ronces enflammées (238, 239), Laser (251), Plateforme lance-flammes (309), etc. Presque tous n'ont que A0 et E0 natifs ; les clés qui manquent vraiment sont D34 (ronces), E14 (rocher tombant), C66, C67 et E18 (boules de fer), C70 et E20 (torche).")
w("")

w("## 5. Les gestionnaires natifs")
w("")
w("### 5.1 Formes (FAIT, comparaison d'octets dans le binaire)")
w("")
w("- **Fonctions vides** (deux mots, `jr ra ; nop`) : A254, C24, C57, D25. Rien à porter ; le no-op de la DLL est déjà exact.")
w("- **Corps identiques à E0/E1** (`DestroyEntity(e, -1)`, 8 mots, md5 égal) : E0, E1, E6, E7, E8, E10, E27. La DLL traite 0, 1 et 10 en ligne. Types de E6, E7 et E27 : 420, 423 et 464 (tous sans enregistrement) ; E8 n'a aucun type. Ajouter 6, 7, 8 et 27 à `IsInlineDeactivateCode` tient en une ligne.")
w("- **E11 n'est pas un alias** : `0x8007F25C` appelle `0x8003A648` (destruction récursive des entités enfants, butin, effet). Atteint par 8 types : 7 par leur en-tête (Melzas, Ronan, Après Melzas, Monsieur Aspiration, Dragon, Mille-pattes, Zorgia) et Zazan par un `0x41 [E,11]` de son programme C.")
w("- **D en forme `HitReaction`** (25 mots, appels `ComputeNewHp` + `GetDirectionToTarget`, `Bytes[3]=1`, animation constante) : D1 (5), D6 (6), D7 (7), D8 (4), D9 (2), D11 (4), D13 (3), D17 (8), D19 (8), D24 (7). `AlundraEnemyHelpers.HitReaction` existe. Corps identiques deux à deux : D7=D24, D8=D11, D17=D19. D2 (76 mots), D3 (42), D5 (62), D10 (32), D12 (74) sont des variantes plus longues.")
w("- **A en setters** : A1 (anim 2), A3 (4), A4 (9), A5 (7), A6 (3), A7 (9), A9 (10), A10 (6), 3 mots chacun ; A8, A14, A17 : 5-6 mots ; A2 : 21 mots (animation aléatoire). A4 et A7 ont le même corps. A11, A15, A21 et A22 appellent `SpawnWarpEntity`.")
w("")
w("### 5.2 Partage des codes entre types ennemis")
w("")
share = []
for k in sorted(H, key=lambda k: (H[k]["slot"], H[k]["code"])):
    h = H[k]
    users = [t for t in set(h["hdrTypes"]) | set(h["types"]) if R[t]["role"] in ENEMYISH + ("projectile/effect (hostile)",)]
    if len(users) >= 2 and k != "A0" and k not in ("E0", "E1") and h["addr"]:
        reg_types = [x for x in h["implTypes"] if common.impl_status(impl, h["slot"], h["code"], x) != "none"]
        state = "vide (`jr ra`), rien à porter" if h["words"] == 2 else (",".join(str(x) for x in reg_types) or "-")
        share.append((k, h["words"], HR[k]["short_handler_effect"][:34], ", ".join(nm(t) for t in sorted(users))[:130], state))
w(table(["clé", "mots", "forme", "types qui l'utilisent", "déjà inscrit pour"], share))
w("")
w("### 5.3 Clés natives les plus utilisées et leur état")
w("")
rows = []
for k, h in sorted(H.items(), key=lambda kv: -kv[1]["recs"]):
    if h["recs"] < 40 or not h["addr"]:
        continue
    cl = ", ".join(f"{a} {b}" for a, b in collections.Counter(R[t]["role"].split(" ")[0] for t in h["types"]).most_common(3))
    rows.append((k, h["func"].split(".")[-1] or "(mot nul)", h["recs"], len(h["maps"]), len(h["types"]), cl, "oui" if h["implTypes"] else "non"))
w(table(["clé", "fonction de la décompilation", "enreg.", "cartes", "types", "rôles", "porté (au moins un type)"], rows))
w("")
w("Les gros consommateurs hors ennemis (E2 caisses et jarres, C23, A255, F255 coffres) relèvent d'E20.g et du butin, pas d'E14.")
w("")
w("### 5.4 Gardes de nom de la décompilation")
w("")
w(f"`FunctionType{{A,C,D,E}}.cs` et `Boss/*.cs` contiennent {guard_total} gestionnaires dont une garde `entity.Name != \"...\"` appelle `Breakpoint.TriggerBreak()` si le nom n'est pas dans la liste. **FAIT** : {guard_ok} de ces listes égalent exactement l'ensemble des types dont l'en-tête (ou un `0x41`) nomme ce code ; les {guard_total - guard_ok} autres sont des listes incomplètes (D2 sans la lance, D6 sans Poisson, D11 sans Loup-garou, E11 sans Après Melzas ni Mille-pattes) ou plus larges (colonnes `guard_names_not_in_census` et `census_types_not_in_guard` de `handlers.tsv`). **INFÉRENCE** : ce sont des assertions de débogage de l'auteur de la décompilation, pas une source de vérité ; l'en-tête et le binaire tranchent. **INCONNU** : E4, E8, E15 et E27 citent aussi « Homme-lézard (épée) » alors qu'aucun en-tête, aucun programme et aucun `0x41` de la base ne mène ce type à ces codes (`scan41.py` : aucun `0x41` vers E4, E8, E15, E27).")
w("")

w("## 6. Prérequis partagés")
w("")
w("Aides du binaire appelées par les clés manquantes des ennemis et des boss et **absentes de la DLL** (recherche de l'adresse dans `Alundra/Scripts/*.cs`, aucune occurrence ; `CreateEffectEntity` existe mais seul l'effet de bris est porté) :")
w("")
mh = collections.OrderedDict([("0x800808DC (sans nom dans la décompilation ; appelle GetEntityTileHeight)", "0x800808dc"), ("CanMoveForward 0x80080260", "CanMoveForward"),
                              ("SetEntityRandomDirection 0x80080030", "SetEntityRandomDirection"), ("TryAttackPlayer 0x80080704", "TryAttackPlayer"),
                              ("GetEntityTileHeight 0x8003AB54", "GetEntityTileHeight"), ("CreateEffectEntity 0x8003BDD8 (autres effets que le bris)", "CreateEffectEntity")])
rows = []
helper_counts = {}
for label, key in mh.items():
    ks = sorted(k for k, h in H.items() if any(n.split(" (")[0] == key for n in h["calls"]) and any(R[t]["role"] in ENEMYISH for t in set(h["hdrTypes"]) | set(h["types"])))
    ty = sorted({t for k in ks for t in set(H[k]["hdrTypes"]) | set(H[k]["types"]) if R[t]["role"] in ENEMYISH})
    helper_counts[key] = (len(ks), len(ty))
    rows.append((label, len(ks), len(ty), " ".join(ks[:12]) + (" ..." if len(ks) > 12 else "")))
w(table(["aide", "clés qui l'appellent", "types ennemis", "clés"], rows))
w("")
w("Déjà présentes (E14.a0b, `AlundraEnemyHelpers.cs`, vérifiées contre l'émulateur MIPS) : position relative `0x8007FE20`/`0x8007FE8C`, `TryAttackPlayer2`, `TryAttackPlayerFront`, `HandleAnimationDirection`, `StartWalk`, `Below(n)`, `HitReaction`. Présents ailleurs : `ComputeNewHp` (`AlundraDamage.cs`), `GetDirectionToTarget` (`ScriptHelper.cs`), `SpawnWarpEntity` par indice de sprite (`IEntityWorldContext.SpawnEntityBySpriteIndex`, E20.e0a1), le générateur et son avance par tick (E20.e0b1, `AlundraWorldProxy.cs:2658,3001`), l'effet de bris (`AlundraNativeEffects.CreateBreakEffect`).")
w("")
w("Dépendances hors E14 :")
w("")
w("- **Butin au décès** (`SpawnEntityContents` `0x80032B90`, appelé par `DestroyEntity`) : E20.e0c, non faite (D-E14-3). Tant qu'elle ne l'est pas, tout ennemi tué ne lâche rien. Les champs d'en-tête `Contents` et `BreakEffect` sont exportés (`alundra-project/Data/sprite-records.json`).")
w("- **Blessure du héros au contact** : E20.c2b, non faite (`docs/plan-e20-heros.md:727`, `:2343`). Nécessaire à toute recette où l'ennemi doit blesser ; inutile pour porter ses gestionnaires.")
w(f"- **Effets** : `CreateEffectEntity` autre que le bris ({helper_counts['CreateEffectEntity'][0]} clés d'ennemis et de boss, {helper_counts['CreateEffectEntity'][1]} types), `CheckAndTriggerTileEffect` (bouchon nommé dans `AlundraWaveSwordAi.cs`) : E20.e.")
w("- **Destruction récursive des enfants** (`0x8003A648`, E11) : nouvelle, pour 8 types de boss.")
w("- **Création d'entités par les gestionnaires** : C7 et C8 créent un sprite de leur propre type (401 et 395) d'après la décompilation ; les créateurs A11, A15, A21, A22 et les clés de boss créent des enfants (colonne `calls_SpawnWarp/SpawnChild` de `handlers.tsv`).")
w("")

w("## 7. Ordre de tranches proposé")
w("")
w("Principes : (1) chaque tranche reprend la méthode d'E14.a1 (oracle MIPS appel par appel, arcs, vérificateur frais) ; (2) un type n'est activé qu'avec **toutes** ses clés (le piège des 118 slimes, `dll-natives.md` §0.3) ; (3) l'histoire connue d'abord (S1, S2), ensuite le rendement (cartes couvertes par millier de mots, colonne `c/kmots`, S3 à S10), puis les boss ; (4) l'ordre réel de l'histoire au-delà du jour 4 est **INCONNU** : l'auteur peut réordonner S3 à S10 et B1 à B14 sans casser les dépendances ci-dessous.")
w("")
rows = []
for s in slices:
    kids = ",".join(str(k) for k in s.get("children", []))
    linked = ",".join(str(k) for k in s.get("nameLinked", []))
    rows.append((s["id"], s["label"], ", ".join(str(t) for t in s["types"]), s["records"], s["maps"], ",".join(map(str, s["storyMaps"])) or "-", " ".join(s["keys"]) or "-", s["words"],
                 s["perKiloWord"] if s.get("perKiloWord") is not None else "-", ", ".join(s["missingHelpers"]) or "-", (kids + (f" (+ par le nom : {linked})" if linked else "")) or "-", s["deps"]))
w(table(["tranche", "contenu", "types", "enreg.", "cartes", "cartes de la chaîne", "clés à ajouter", "mots", "c/kmots", "aides absentes", "enfants sans enregistrement", "dépend de"], rows))
w("")
bs = [s for s in slices if s["id"].startswith("B")]
w("**S0, socle (avant toute famille).** (a) Inscription cumulative des types par clé dans `AlundraNativeSpriteRegistry` (aujourd'hui une seconde inscription lève) ; (b) `IsInlineDeactivateCode` : 6, 7, 8, 27 ; (c) table des `HitReaction` (D6, D7, D8, D11, D13, D17, D19, D24 : une ligne chacun, `AlundraEnemyHelpers.HitReaction` existe) ; (d) setters A en table ; (e) E3 (mort des projectiles, 25 mots) et C36 (boomerang, 24 mots) ; (f) les aides de la section 6, chacune avec ses vecteurs d'oracle ; (g) l'interrupteur des natifs existe déjà (`NativeHandlersEnabled`). Valeur : D6 et D11 réclamés par 16 types, D5 par 6, D2 par 4. S0 ne change aucun comportement visible (registre vide pour les nouveaux types).")
w("")
w(f"**S1 et S2 d'abord**, parce que la chaîne les touche (cartes 14 et 15 au jour 2 ; carte 44 au jour 3). **S3** (Slime gélatineux) est le meilleur rendement : A1 et D1 existent, il faut les rouvrir au type 329 (sinon les 118 slimes ne meurent pas, `dll-natives.md` §0.3), ajouter C26 et le projectile 330. S5 porte le plus d'enregistrements ; deux de ses clés (C7, C8) créent des entités. Les **boss** B1 à B{len(bs)} passent en dernier, chacun avec son oracle : ordre par nombre de cartes décroissant puis taille croissante (données, pas histoire) ; leurs clés pèsent de {min(s['words'] for s in bs)} à {max(s['words'] for s in bs)} mots, avec {min(s['rng'] for s in bs)} à {max(s['rng'] for s in bs)} sites du générateur et {min(s['spawnCalls'] for s in bs)} à {max(s['spawnCalls'] for s in bs)} appels de création. Les enfants sans enregistrement 489-492, 504 (Mille-pattes) et 496, 502, 503 (Nirude) n'ont pas de parent littéral dans la décompilation : rattachés par le nom (**INFÉRENCE**) à B7 et B10.")
w("")
w("Dépendances croisées à respecter : les enfants (4.3) après leur parent ; D2 partagé entre S8 et Zazan (B5), donc B5 après S8 ; « Petit slime » (478, S10) réutilise C1 du Beannoïde et « Sbires du roi » (508, B5) C10 du Muruta, d'où l'inscription cumulative de S0 ; E20.c2b avant toute recette qui compte les points de vie du héros ; E20.e0c avant toute recette qui compte les objets lâchés ; E20.e pour les effets autres que le bris.")
w("")

w("## 8. Limites et inconnues")
w("")
w("- **INCONNU** : l'ordre de l'histoire hors de la chaîne de 37 cartes. `1re carte histoire` est vide pour les types qui n'y apparaissent pas ; `prof.` n'est qu'un indice (portails et `0x53` lus comme des numéros de carte, vérifié seulement sur la chaîne 389 vers 163).")
w("- **INFÉRENCE** : les étendues de fonctions (« mots ») sont des majorants (début de la fonction suivante, resserré par un contrôle de `jr ra`). Elles retrouvent 223 mots pour C1 et 503 pour C10, comme l'annexe.")
w("- **INFÉRENCE** : la colonne `short_handler_effect` (constantes écrites en `entity+0x88`) ignore les branchements ; E2 y paraît écrire -1 alors que la constante est 2. Relire le binaire avant de porter.")
w("- **INFÉRENCE** : le balayage linéaire des programmes (`0x40`, `0x41`, `0x53`) borne chaque programme au début du suivant ; il n'a rencontré aucun opcode inconnu. Il ne suit pas `0x43` (changement d'entité logique) : un `0x40`/`0x41` visant une autre entité est attribué au type du programme. Les entrées de la table F sont lues seulement pour les index réellement utilisés (la table n'a pas de taille enregistrée).")
w("- **INCONNU** : pourquoi la décompilation cite le lézard (épée) aux gardes d'E4, E8, E15, E27.")
w("- Les types `\u25c6` sans bits de combat (419, 477, 493, 499 et quelques projectiles de boss) sont classés « objet rattaché à un ennemi » sur le seul nom ; presque aucun enregistrement.")
w("")

w("## 9. Fichiers (dossier de travail)")
w("")
w("Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/themes/enemies2/`.")
w("")
w(f"- Sorties : `roster.tsv` ({n_headers} types), `handlers.tsv` ({len(handler_rows)} clés), `slices.tsv`, `report.md`.")
w("- Scripts (`python -B`) : `exelib.py`, `mipsdis.py` (lecture et désassemblage de l'EXE), `exe_handlers.py` (tables, étendues, appels), `decomp_index.py` (adresses, enregistrements, sites de création, gardes de nom), `extract_maps.py` (cache des cartes), `common.py` (noms, DLL, balance), `census.py` (recensement, `0x40`/`0x41`/`0x53`), `scan41.py` (balayage global des `0x41`), `build_outputs.py` (classification, TSV), `slices.py`, `report_gen.py`. Ordre : `extract_maps.py`, `exe_handlers.py`, `census.py`, `build_outputs.py`, `slices.py`, `report_gen.py`.")
w("- Cache : `cache/` (cartes extraites, désassemblage de l'EXE, résultats intermédiaires).")

with open(os.path.join(HERE, "report.md"), "w", encoding="utf-8") as fh:
    fh.write("\n".join(L) + "\n")
print("report.md written,", len(L), "lines")
