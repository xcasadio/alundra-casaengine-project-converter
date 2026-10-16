#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Assemble opcodes.tsv (one row per opcode 0x00-0xFF) from the intermediate files of this scratch folder (label script2).

Inputs (all produced by the scripts of this folder, read-only on the repository):
  cases.json, handler-doc.json, handler-addresses.tsv, size-check.tsv, decomp-handlers.json, tests-by-opcode.json, tests-instr.json,
  plan-mentions.json, corpus_by_opcode.json, skipped-sites.tsv
Manual knowledge (states, notes, plan status) is in the dictionaries below; each entry cites a file:line or a plan section.
"""
import json, os, re, csv, collections

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
OUT = os.path.dirname(os.path.abspath(__file__))
RUNNER = 'Alundra/Scripts/AlundraEventProgramRunner.cs'

def jl(name):
    return json.load(open(os.path.join(OUT, name), encoding='utf-8'))

src = open(REPO + '/Alundra/Scripts/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES, NAMES = {}, {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', src):
    SIZES[int(m.group(1), 16)] = int(m.group(2)); NAMES[int(m.group(1), 16)] = m.group(3)

cases = {int(k, 16): v for k, v in jl('cases.json').items()}
hdoc = {int(k, 16): v for k, v in jl('handler-doc.json').items()}
decomp = {int(k, 16): v for k, v in jl('decomp-handlers.json').items()}
tests_n = {int(k, 16): v for k, v in jl('tests-by-opcode.json').items() if not k.startswith('_')}
tests_i = {int(k, 16): v for k, v in jl('tests-instr.json').items() if not k.startswith('_')}
planm = {int(k, 16): v for k, v in jl('plan-mentions.json').items()}
corp = {int(k, 16): v for k, v in jl('corpus_by_opcode.json').items()}
binev = {int(k, 16): v for k, v in jl('bin-evidence.json').items()}

haddr = {}
for l in open(OUT + '/handler-addresses.tsv').read().split('\n')[1:]:
    if l:
        a, b = l.split('\t'); haddr[int(a, 16)] = int(b, 16)
sizecheck = {}
for r in csv.DictReader(open(OUT + '/size-check.tsv'), delimiter='\t'):
    sizecheck[int(r['opcode'], 16)] = r
# handler span (upper bound on the binary handler length): distance to the next handler address
sorted_addrs = sorted(set(haddr[i] for i in range(0xC5)))
def span(op):
    a = haddr[op]
    for x in sorted_addrs:
        if x > a:
            return x - a
    return 0

DEFAULT_STUB = 0x8003D158

# ---------------------------------------------------------------------------------------------------------------------
# Manual states. Everything with a case in Dispatch is FAITHFUL unless listed here.
# ---------------------------------------------------------------------------------------------------------------------
NOOP = {0x01, 0xA8, 0xBA}
PARTIAL = {0x82, 0xA4, 0xAC, 0x53, 0x2F}

NOTE = {
    0x00: "Break : boucle d'appel (RunOneScriptCall), Parameters[1]=0, CodeIndex++, fin d'appel ; reprise a l'instruction suivante.",
    0xFF: "End script : boucle d'appel, fin d'appel (EndCall).",
    0x01: "Rend 1 (no-op). Binaire : gestionnaire par defaut 0x8003D158 = message « Data Logic Error! » puis retour 0 (l'appel s'arrete, pc non avance) ; M-03 en attente de l'auteur ; 0 site corpus.",
    0x0B: "Binaire 0x8003D468 ; le port AJOUTE un detour de navigation (E4.d, D-E19-6) absent de l'original.",
    0x1E: "Avec detour de navigation (E4.d) ajoute par le port ; 0x1F est la meme marche sans detour.",
    0x24: "Fidele ; sa fin depend du rognage au bord de la carte (O-E19-17, non porte) et du recensement D5 (O-E19-16 : 26/59 sites de la chaine non conclus en modele statique).",
    0x2F: "Modes 0 (Hold) et 1 (JustPressed) ; autres modes : Result 0 + journal. Corpus : 341 sites mode 0, 11 mode 1, 0 mode >= 2.",
    0x38: "Garde d'indice hors table (ecart documente ; le binaire ecrit sans controle).",
    0x40: "Leve g_clearProgramState ; v1 >= 6 ignore avec avertissement (le binaire ecrirait hors tableau ; 0 site). Corpus : 426 sites, TOUS posent l'index 0 (= remise au gestionnaire natif).",
    0x41: "SpriteProgramIndexes[v1] = v2 ; v1 >= 6 ignore avec avertissement.",
    0x53: "Branche effet 3 (teleportation meme carte) gelee, journalisee, 0 site sur 333 du corpus (T7) ; le reste est le warp via AlundraWarpDirector.",
    0x64: "PosZ sans le +1 du binaire (convention DLL, ADR-0026, D-E19-59).",
    0x8A: "PosZ sans le +1 du binaire (convention DLL) ; spawn echoue : journal, le binaire s'arrete.",
    0x2D: "Spawn echoue : journal (le binaire s'arrete) ; JoinsBirthTick (E20.e0a4).",
    0x8B: "Spawn echoue : journal (le binaire de la decompilation dereference null) ; JoinsBirthTick (E20.e0a4).",
    0x5A: "Parcours des entites trouvees de la derniere a la premiere comme le binaire (M-47, E19.m1).",
    0x5B: "Idem 0x5A.",
    0x82: "HandleMapTriggerCommand : ids 0, 0x45-0x48, 0x4F, 0x53 et objets portes ; sous-cas 0x50-0x52 et 0x54-0x56 DEGRADES (Result 1, rien d'ecrit, trace Degraded ; O-E19-39). Corpus : aucun site de ces sous-cas (ids vus : 0x4F x53, 0x53 x8 et objets).",
    0xA4: "Masque des couches de fond applique ; le cycle de palettes (v2 > 0) n'est PAS porte (trace Degraded, O-E19-43). Corpus : 1 site v2>0 (carte 471), 20 sites v2=0.",
    0xAC: "Ecrit les bits de taille d'ombre de Flags de la 1re correspondance ; AUCUN rendu d'ombre (ni DLL ni moteur, grep 2026-10-17) et aucun propriétaire dans les plans.",
    0xA8: "Result = 0 constant (pas de lecture CD, D-E11-5).",
    0xBA: "Result = 0 constant (D-E11-5) ; O-E19-36 (pistes XA) le rendrait « occupe » tant qu'une piste joue.",
    0x85: "Pas de borne (ecart documente : le test de borne de l'original est un piege de debogage).",
    0xB0: "Les couleurs sont mortes (aucun lecteur) ; seule la duree sert, lue par 0xB1.",
    0x0D: "Rend 0 (reessaie) tant qu'une boite est ouverte ; sans presentateur : joue le noeud sans affichage (pose les drapeaux).",
    0x5C: "Idem 0x0D ; v1 = recherche du locuteur (E19.f4b).",
    0xC4: "Idem 0x0D ; v1 locuteur, v2|v3<<8 nom, v4 texte, v5 mode.",
    0x39: "Attente de fermeture de la boite (poll pur).",
    0x44: "Ouvre OUI/NON ; sans presentateur : Result 1 et avance.",
    0x87: "Lit TouchingEntity/attaque courante ecrits par la passe de coups (E20.c1a) ; table de 12 octets du binaire.",
    0x95: "Lit la boite de coup stockee (StoreHitBox) ; index de type >= 8 lit au-dela de la table comme le binaire.",
    0x57: "Sans heros : degrade (d = 0, avertissement).",
    0x3B: "Sans heros : Result 0 + avertissement.",
    0x52: "Sans heros ou sans hote : Result 0 + avertissement.",
    0x27: "Sans heros : direction inchangee.",
    0x3E: "Sans heros : Result 0 + avertissement.",
    0x22: "Cible = Height<<19 − 1 (ADR-0026) ; entite sans enregistrement : avertissement et fin d'attente ; O-E19-68 (descente d'un tick trop tot, 1 site hors chaine).",
    0x62: "Liste d'un tick de retard pour 0x62/0x63 (O-E19-28).",
    0x63: "Idem 0x62.",
    0x1C: "Compteur de passages du binaire (pas celui de la decompilation) ; garde sous rattrapage.",
}

# Semantics of the opcodes the DLL skips or terminates on (decompilation EntityEventHandlers.cs; B4-B6 labels checked in the binary).
SEM = {
    0x15: "Reset z pos : PosZ = Height(record)*8 − ModZ (decomp.) ; binaire 0x8003D710 : message d'erreur si pas d'enregistrement.",
    0x3C: "Predicat : une entite Chargee/Normale/Desactivee avec AnimFlags NoEntityCollision, sans Collidable, sans PlatformEntity, dans la boite de cases v1..v6 -> Result.",
    0x3D: "Predicat : une entite Chargee/Normale/Desactivee dans la boite de cases v1..v6 -> Result.",
    0x56: "ChangeAreaTileProperties(MapCopies[v1]) : copie d'un rectangle de cases par la table MapCopies de la carte (non exportee, D-E7-2).",
    0x5F: "Attente animation/distance sur l'entite LOGIQUE (binaire 0x8003F228) ; taille 8 (binaire, E19.m8 M-01) ; handler non porte.",
    0x60: "Predicat : l'une des entites trouvees par v1 a RidingEntity == entite logique (interrupteurs au sol / plateformes).",
    0x61: "Predicat : l'entite logique a pour RidingEntity l'une des entites trouvees par v1.",
    0x66: "Copie le contexte de l'owner dans l'etat global, recopie Target* -> LastTarget*, reecrit CodeIndex de l'owner depuis le programme (sens de la copie faux dans la decompilation, O-E19-2).",
    0x6A: "Memorise PosX/PosY/PosZ de l'entite logique dans Parameters[2..4].",
    0x6B: "Predicat : |Parameters[2] − PosX| >= (v1|v2<<8) px -> Result 1 (distance depuis 0x6A).",
    0x6C: "Predicat : |Parameters[3] − PosY| >= (v1|v2<<8) px -> Result 1.",
    0x6D: "Predicat : |Parameters[4] − PosZ| >= (v1|v2<<8) px -> Result 1.",
    0x72: "LastTargetAnimationId/LastTargetDirection = TargetAnimationId/TargetDirection de l'entite logique.",
    0x7A: "Miroir de 0x79 : si Result == 0, _34 = pc+3 et saut relatif (v1|v2<<8) ; sinon avance 3.",
    0x88: "Statut des entites trouvees par v1 := table[v2] (octets en 0x80023D2C).",
    0x96: "PV += v1 des entites trouvees (la decompilation reutilise v1 comme type de recherche ET montant : suspect), plafonne a HpMax.",
    0x97: "SpendMoney(v1) : argent −= octet v1.",
    0x9E: "Predicat : nombre de cases avec Walkability & 2 dans le rectangle (v1,v2,v3,v4) >= v5 -> Result.",
    0xA9: "Predicat : XCollisionEntity de l'entite logique est l'une des entites trouvees par v1.",
    0xAA: "Predicat : l'une des entites trouvees par v1 a XCollisionEntity == entite logique.",
    0xB2: "Predicat : les groupes trouves par v1 et v2 ont une entite commune.",
    0xB3: "Ecrit g_padState1 (Hold, JustPressed, Released, ByInterval) depuis v1..v8 : manette virtuelle.",
    0xB4: "Predicat sur la 1re entite trouvee par v1 : ForceZ > 0 (verifie dans le binaire 0x800417CC ; le libelle de la table « < 0 » est FAUX).",
    0xB5: "Predicat sur la 1re entite trouvee par v1 : ForceZ < 0 (binaire 0x80041830 ; le libelle « >= 0 » est FAUX).",
    0xB6: "Predicat sur la 1re entite trouvee par v1 : ForceZ == 0 (binaire 0x80041894 ; le libelle « != 0 » est FAUX).",
    0xB7: "Predicat : TargetAnimationId == v2 pour une entite trouvee par v1 (binaire 0x800418F8 : recherche = v1).",
    0xB9: "StartCdStreaming(v1) : lance la piste XA v1 (table de 13 entrees 0x800C4818) ; fanfare apres chaque objet.",
    0xBB: "Ecran « Reessayer » de la carte 0x1DD : compte 60 ticks puis recharge la sauvegarde en memoire (effet 10) -> E18.",
    0xC2: "Predicat : SaveSlotIndex (compteur de reprises apres la mort) >= v1 (E18).",
    0x0E: "Gestionnaire binaire = message d'erreur, retour 0.",
    0x0F: "Idem 0x0E.",
    0x13: "Idem 0x0E.",
    0x14: "Gestionnaire binaire 0x8003D6EC = message d'erreur (autre chaine), retour 0.",
    0x18: "Idem 0x0E.",
    0x76: "Gestionnaire binaire 0x8003FAC8 = message d'erreur, retour 0.",
    0x77: "Gestionnaire binaire 0x8003FAEC = message d'erreur, retour 0.",
    0x9D: "Gestionnaire binaire 0x80040B88 = message puis exit(1) (0x800831E0).",
    0xAE: "Gestionnaire binaire 0x800414B4 = message puis exit(1).",
}

# Plan ownership / status (docs/plan-e19-opcodes.md unless said otherwise)
def own(slice_, status):
    return (slice_, status)

PLAN = {}
def setplan(ops, slice_, status):
    for o in ops:
        PLAN[o] = (slice_, status)

setplan([0x42, 0x43, 0x59], 'E19.a §1.2', 'ok (CONFIRMED, recette validee 2026-10-01)')
setplan([0xC4, 0x8A], 'E19.b §1.2d', 'ok (recette validee 2026-10-02)')
setplan([0x0B, 0x08, 0x0C, 0x3A, 0x5E, 0x89, 0x73, 0x74], 'E19.c1 §1.2e', 'ok (recette validee 2026-10-02)')
setplan([0x1C, 0x1D], 'E19.c1 + E19.c2 §1.2e-f', 'ok (recette validee 2026-10-02) ; compteur du binaire')
setplan([0x24, 0x40, 0x41], 'E19.d §1.2g (D4/D6)', 'ok ; 0x24 : O-E19-16/17/21 ouverts')
setplan([0x25, 0x1B], 'E19.d2c1 §1.2h.3', 'ok (CONFIRMED) ; recette C7 en attente')
setplan([0x21, 0x26, 0x47, 0x48, 0x6F], 'E19.h1 §1.2n', 'ok (CONFIRMED) ; recettes en attente')
setplan([0x20, 0x22, 0x23], 'E19.h1b2 §1.2n.1c', 'ok (CONFIRMED) ; O-E19-68 (0x22 descente)')
setplan([0x45, 0x46], 'E19.h4 §1.2n', 'ok (recette en attente)')
setplan([0x8E, 0x8F], 'E19.k1 §1.2k', 'ok (recette K5 : 392 et 391 validees) ; O-E19-35 (ecrivains natifs -> E14)')
setplan([0xA4], 'E19.k2 §1.2k', 'masque porte, recette K2-4 en attente ; cycle de palettes = O-E19-43 (plus tard, moteur)')
setplan([0x28, 0x29, 0x2A, 0x2B, 0x3F, 0x4A, 0x52, 0x57, 0x58, 0x5D, 0x8C], 'E19.l1 §1.2m.1', 'ok (recette L1-4 : Inoa 164 validee)')
setplan([0x82, 0x83, 0x84, 0x98, 0x99, 0x9A, 0x9F], 'E19.l2 §1.2m', 'ok ; 0x82 sous-cas 0x50-0x52/0x54-0x56 : O-E19-39 ouvert')
setplan([0x90, 0x91, 0x92, 0x93, 0x94, 0xA0, 0xA1, 0xA2, 0xA3], 'E19.g §1.2o (G3b)', 'portes (G3b OK) ; E19.g reste en cours : recettes G2a-4/G2b-4/G2c-6/G2d-6 en attente')
setplan([0x0D, 0x39, 0x44, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x5C], 'E19.f §1.2j (f2a/f4b)', 'handlers portes ; E19.f en cours : f2a/f2b0/f2b1 en recette')
setplan([0x71, 0x86, 0x87, 0x95], 'E20.c1b K4 (plan-e20-heros.md §1.6)', 'ok (CONFIRMED) ; O-E19-37 clos de fait')
setplan([0xC0, 0xC1], 'E20.f0 T1 (plan-e20-heros.md)', 'ok')
setplan([0xBC, 0xC3], 'E20.c2a T3 (plan-e20-heros.md)', 'ok')
setplan([0x05, 0x06, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x7B, 0x7C, 0x80, 0x81, 0x78, 0x79, 0x7D, 0x7E, 0x7F, 0xAD, 0xB8, 0x8D], 'E16.a (plan-e16-etat-partie.md)', 'ok (CONFIRMED)')
setplan([0x5F], 'E19.m8 M-01 (taille)', 'taille 8 corrigee ; handler non porte ; 0 site ; rangé parmi les attentes (regle 3 de l audit)')
setplan([0x53, 0x9B, 0x9C], 'plan-transitions-carte.md T7', 'ok ; branche effet 3 gelee (0 site)')
setplan([0xAF, 0xB0, 0xB1], 'E10.b (plan-e10-fondu.md)', 'ok')
setplan([0xBD, 0xBE, 0x12, 0x75, 0xAB, 0xBF], 'E11/E11b (plan-e11-audio.md, plan-e11b-opcodes-audio.md)', 'ok')
setplan([0xA5, 0xA6, 0xA7], 'plan-bgm-demarrage-binaire.md D8', 'ok (CLOS, valide en jeu)')
setplan([0x54, 0x55, 0x85], 'E7 (plan-e7-mutation-tuiles.md)', 'ok')
setplan([0x3B, 0x2C, 0x3E], 'E7.c / E1', 'ok')
setplan([0x67, 0x68, 0x69], 'E5.a (plan-e5-camera.md)', 'ok')
setplan([0x10, 0x11], 'E6 (plan-conversion-totale.md)', 'ok')
setplan([0x16, 0x17, 0x19, 0x1A, 0x1E, 0x1F, 0x27, 0x5A, 0x5B, 0x62, 0x63, 0x64, 0x65, 0x6E, 0x70, 0xAC], 'E1/E4 (plan-e4-deplacement-scripte.md)', 'ok (portes de la decompilation) ; 0xAC sans rendu d ombre')
setplan([0x02, 0x03, 0x04, 0x07, 0x09, 0x0A, 0x2D, 0x2E, 0x37, 0x38, 0x49, 0x4B, 0x01], 'E1 (plan-conversion-totale.md E1)', 'ok (portes de la decompilation)')
setplan([0xA8, 0xBA], 'E11 (plan-e11-audio.md, D-E11-5)', 'ok (constante 0, pas de CD) ; O-E19-36 pourrait changer 0xBA')
setplan([0x2F], 'E7.c (plan-e7-mutation-tuiles.md D-E7-7..9) + E20.a A1', 'ok (modes 0/1) ; modes >= 2 sans champ (0 site)')
setplan([0x8B], 'E1/E2 (apparition par script) + E20.e0a4', 'ok')
# Unported
UN_PLAN = {
    0x15: ('aucun', "jamais cite dans les plans ; 0 site"),
    0x3C: ('aucun', "jamais cite ; 0 site"),
    0x3D: ('aucun (M-05)', "recensement G7 du 2026-10-03 : « sans proprietaire » ; 1 site"),
    0x56: ('hors perimetre E7 (D-E7-2)', "table MapCopies non exportee ; « sans proprietaire » dans G7 (M-05)"),
    0x60: ('aucun (M-05)', "« sans proprietaire » (172 sites) dans le recensement G7"),
    0x61: ('aucun (M-05)', "« sans proprietaire »"),
    0x66: ('aucun', "§0.2.3 : ne change pas l'entite logique ; O-E19-2 : sens de la copie ; 0 site"),
    0x6A: ('aucun (M-05)', "« sans proprietaire »"),
    0x6B: ('aucun (M-05)', "« sans proprietaire »"),
    0x6C: ('aucun (M-05)', "« sans proprietaire »"),
    0x6D: ('aucun (M-05)', "« sans proprietaire »"),
    0x72: ('aucun (M-05)', "« sans proprietaire »"),
    0x7A: ('aucun', "audit E19.e : « n'est pas porte : saute par sa taille 3 » ; 0 site"),
    0x88: ('aucun (M-05)', "« sans proprietaire »"),
    0x96: ('aucun (M-05)', "« sans proprietaire » ; O-E19-39 : la vie du heros est l'entite 0, g_playerStats son miroir (G3d) -> maintenant portable"),
    0x97: ('aucun (M-05)', "« sans proprietaire » ; AddMoney existe (E19.l2)"),
    0x9E: ('aucun', "jamais cite ; 0 site"),
    0xA9: ('aucun (M-05)', "« sans proprietaire »"),
    0xAA: ('aucun (M-05)', "« sans proprietaire »"),
    0xB2: ('aucun (M-05)', "« sans proprietaire »"),
    0xB3: ('aucun (M-05)', "« sans proprietaire »"),
    0xB4: ('aucun', "jamais cite ; 0 site ; libelle de la table faux"),
    0xB5: ('aucun (M-05)', "« sans proprietaire » ; libelle faux"),
    0xB6: ('aucun (M-05)', "« sans proprietaire » ; libelle faux"),
    0xB7: ('aucun', "jamais cite ; 0 site"),
    0xB9: ('auteur (O-E19-36, Q-A1)', "XA : porter (decoder l'audio du .bin brut + lecteur de flux ponctuel moteur), ou sans-effet journalise, ou laisser saute ; question non tranchee"),
    0xBB: ('E18 (hors E19)', "plan-conversion-totale.md E18 : Reessayer ; hors perimetre §4"),
    0xC2: ('E18 (plan-conversion-totale.md) / G7 « sans proprietaire »', "la decompilation ne lit qu'un octet de parametre (E18)"),
}

# DLL source location of non-case behaviours
LOOP_LOC = {0x00: RUNNER + ':459-466', 0xFF: RUNNER + ':452-457'}

# -------------------------------------------------------------------------------------------------------------------
rows = []
HEADER = ['opcode', 'name_in_size_table', 'size_table', 'size_vs_binary', 'binary_handler', 'binary_handler_span', 'dll_state', 'dll_handler',
          'binary_cited_in_dll_comment', 'dll_note', 'semantics_or_binary_behaviour', 'decomp_handler', 'tests_named_methods',
          'tests_programs_with_instruction', 'tests_examples', 'tests_manual_check', 'plan_owner', 'plan_status', 'plan_mentions_e19', 'plan_open_points',
          'corpus_site_programs', 'corpus_unique_sites', 'corpus_maps', 'corpus_slots', 'story_core8_sites', 'story_core8_maps', 'chain30_sites', 'combat7_sites']

TEST_MANUAL = {
    0x30: 'AUCUN test unitaire ne le nomme (verifie par grep) ; couverture indirecte : FlagBranch partage avec 0x31 (AlundraEventProgramRunnerTests.cs:404-433), garde du marin 12 (AlundraDialogueOpcodesProductionTests.cs:17-31), AlundraYarnVariableStorageTests.cs:120, arcs sur vrais programmes',
    0x23: 'verifie a la main : AlundraHeightWaitOpcodesTests.cs:250-298 (section 0x23)',
    0x41: 'verifie a la main : AlundraEventProgramRunnerProgramIndexTests.cs:206 (U11)',
    0x4D: 'verifie a la main : AlundraDialogueOpcodeDispatchTests.cs:320-350',
    0x4E: 'verifie a la main : AlundraDialogueOpcodeDispatchTests.cs:320-350 ; AlundraDialogueBoxOracleComparisonTests.cs:110',
    0x6F: 'verifie a la main : AlundraZWaitOpcodesTests.cs:288-304',
    0xC5: 'AlundraEventProgramRunnerTests.cs:353 (UnknownOpcode_NoKnownSize_TerminatesScriptCall, opcode 0xC5)',
    0x5F: 'AlundraEventProgramRunnerTests.cs:338 (saut de 8 octets) ; regle 3 de AlundraStoryChainOpcodeAudit (attente)',
    0xB3: 'AlundraEventProgramRunnerTests.cs:323 (saut par la taille 9)',
}

OPEN = {
    0x0B: 'O-E19-24 (calage 10), O-E19-25 (recensement des marches)', 0x1E: 'O-E19-17 (rognage de bord), O-E19-25', 0x1F: 'O-E19-17',
    0x24: 'O-E19-16 (D5), O-E19-17, O-E19-21', 0x20: 'O-E19-27 (reste du saut), O-E19-68', 0x21: 'M-29 (test)', 0x22: 'O-E19-68',
    0x25: 'O-E19-29 (troncature au pixel a l atterrissage)', 0x40: 'O-E19-34 (resolu par E19.m9)', 0x82: 'O-E19-39 ; M-28 (test)',
    0xA4: 'O-E19-43, O-E19-56', 0x8E: 'O-E19-35 (ecrivains natifs -> E14)', 0xB9: 'O-E19-36 (Q-A1)', 0xBA: 'O-E19-36', 0x01: 'M-03 (auteur)',
    0x5F: 'M-01 (fait), O-E19-2', 0x66: 'O-E19-2', 0x87: 'O-E19-37 (Q-A2)', 0x95: 'O-E19-37 (Q-A2)', 0x64: 'O-E19-45', 0x8A: 'M-12 (plafond de 63 entites, reuse de creneaux), O-E19-45',
    0x2D: 'M-12, O-E19-23 (butin, E14)', 0x8B: 'M-12', 0x62: 'O-E19-28', 0x63: 'O-E19-28', 0x5A: 'M-47 (fait)', 0x5B: 'M-47 (fait)',
    0x0D: 'E19.f (f2/f3/f4), O-E19-67', 0x5C: 'E19.f', 0xC4: 'E19.f', 0x39: 'M-39', 0x44: 'M-20 (test A17)', 0x58: 'M-24, M-25 (tests)',
    0x57: 'M-25, M-26 (fait)', 0x89: 'M-13 (allocation)', 0x07: 'M-13', 0x52: 'O-E19-80 (pose de saut a l arrivee)', 0x53: 'O-E19-32/E19.v, O-E19-80',
    0x2E: 'O-E19-47 (fait), O-E19-23', 0x2C: 'O-E19-47 (fait)', 0xAC: 'aucun propriétaire (rendu des ombres)', 0x1C: 'O-E19-9 (fait), O-E19-14 (fait), M-14 (test)',
    0x1B: 'O-E19-27, O-E19-29', 0x4E: 'aucun site', 0x4F: 'aucun site',
}

for op in range(256):
    inT = op in SIZES
    size = SIZES.get(op)
    # state
    if op in (0x00, 0xFF):
        state = 'LOOP_BUILTIN'
    elif op in cases:
        state = 'NOOP_DELIBERATE' if op in NOOP else ('PARTIAL' if op in PARTIAL else 'FAITHFUL')
    elif inT and size > 0:
        state = 'SKIPPED_BY_SIZE'
    elif inT:
        state = 'TERMINATES_SIZE0'
    else:
        state = 'NOT_IN_TABLE'
    # handler location
    if op in LOOP_LOC:
        loc = LOOP_LOC[op]
    elif op in cases:
        loc = '%s:%d' % (RUNNER, cases[op])
    elif state in ('SKIPPED_BY_SIZE', 'TERMINATES_SIZE0', 'NOT_IN_TABLE'):
        loc = RUNNER + ':3115 (UnknownOpcode, %s)' % ('saute par sa taille' if state == 'SKIPPED_BY_SIZE' else 'termine l\'appel, retour 0')
    else:
        loc = ''
    cited = ''
    if op in hdoc:
        cited = 'Y' if (hdoc[op]['addresses'] or hdoc[op]['kw']['per_the_binary']) else 'N'
    elif op in (0x00, 0xFF):
        cited = 'Y'
    # binary
    baddr = '%08X' % haddr[op] if op < 256 and op in haddr else ''
    bspan = ''
    if op <= 0xC4:
        bspan = str(span(op))
    sc = sizecheck.get(op)
    svb = ''
    if sc:
        svb = {'agree': 'agree (heuristique)', 'check': 'a verifier (variable/jump)', 'size0-table': 'table=0'}[sc['status']]
    if op == 0x5F:
        svb = 'binaire=8 (E19.m8 M-01)'
    if op in (0x78,):
        svb = 'binaire=3 (D-E16-21)'
    if haddr.get(op) == DEFAULT_STUB:
        svb = (svb + ' ; ' if svb else '') + 'defaut 0x8003D158'
    if op == 0x00:
        svb = 'traite par la boucle RunScript avant la table (0x800422BC) ; entree de table = defaut 0x8003D158'
    if op == 0xFF:
        baddr = 'boucle (0x800422B4)'; svb = 'traite par la boucle avant la table (entree 255 de la table = donnee, pas un pointeur)'
    # decomp
    d = decomp.get(op)
    dec = ('%s (EntityEventHandlers.cs:%s)' % (d['name'], d['line'])) if d else ''
    # tests
    tn = tests_n.get(op, {})
    ti = tests_i.get(op, {})
    named = tn.get('named_methods', 0)
    progs = ti.get('methods', 0)
    if state in ('SKIPPED_BY_SIZE', 'TERMINATES_SIZE0', 'NOT_IN_TABLE'):
        # the hex literal of a skipped opcode only matches test programs by accident, except the two tests of the skip policy
        if op == 0x5F:
            named, progs, ti = 1, 1, {'examples': ['AlundraEventProgramRunnerTests.cs::UnknownOpcode_0x5F_SkipsEightBytes_AsTheBinary (:338)']}
        elif op == 0xB3:
            named, progs, ti = 0, 1, {'examples': ['AlundraEventProgramRunnerTests.cs::UnknownOpcode_KnownSize_SkipsBySize (:323)']}
        else:
            named, progs, ti = 0, 0, {'examples': []}
        tn = {'named_files': []}
    ex = []
    for f in tn.get('named_files', [])[:2]:
        ex.append(f)
    for e in ti.get('examples', [])[:2]:
        ex.append(e.split('::')[0])
    ex = list(dict.fromkeys(ex))[:3]
    # plan
    if op in UN_PLAN:
        pown, pstat = UN_PLAN[op]
    elif op in PLAN:
        pown, pstat = PLAN[op]
    elif state == 'TERMINATES_SIZE0':
        pown, pstat = 'aucun', "taille 0 : ni porte ni cite (le recensement G7 : « aucun opcode atteignable de taille nulle »)"
    elif state == 'NOT_IN_TABLE':
        pown, pstat = 'aucun', 'hors table (binaire : gestionnaire par defaut)'
    elif op in (0x00, 0xFF):
        pown, pstat = 'E1 + E19.a (garde)', 'ok'
    else:
        pown, pstat = '?', '?'
    pm = planm.get(op, {})
    sl = pm.get('slices', {})
    pm_txt = '; '.join('%s:%d' % (k, v) for k, v in list(sl.items())[:3]) if sl else ''
    if op in (0x45, 0x46, 0x47, 0x48, 0x4F) or 0x50 <= op <= 0x56:
        pm_txt += ' [ATTENTION : mentions melangees avec les ids de sous-cas de 0x82 (0x45-0x48, 0x4F, 0x50-0x56)]'
    if pm and pm.get('total'):
        pm_txt = ('%d mentions ; ' % pm['total']) + pm_txt
    # corpus
    c = corp.get(op)
    cs = ''
    if c:
        cs = ' '.join('%s:%d' % (k, v) for k, v in sorted(c['slots'].items()))
    note = NOTE.get(op, '')
    sem = SEM.get(op, '')
    if state in ('FAITHFUL', 'PARTIAL', 'NOOP_DELIBERATE') and not sem:
        sem = NAMES.get(op, '')
    rows.append([
        '0x%02X' % op, NAMES.get(op, '') if inT else '', str(size) if inT else '', svb, baddr, bspan, state, loc, cited, note, sem, dec,
        str(named), str(progs), ' | '.join(ex), TEST_MANUAL.get(op, ''), pown, pstat, pm_txt, OPEN.get(op, ''),
        str(c['site_programs']) if c else '0', str(c['unique_sites']) if c else '0', str(c['maps']) if c else '0', cs,
        str(c['core8_sp']) if c else '0', ','.join(str(x) for x in c['core8_maps']) if c else '',
        str(c['chain30_sp']) if c else '0', str(c['combat7_sp']) if c else '0'])

with open(OUT + '/opcodes.tsv', 'w', encoding='utf-8', newline='\n') as f:
    f.write('\t'.join(HEADER) + '\n')
    for r in rows:
        f.write('\t'.join(x.replace('\t', ' ').replace('\n', ' ') for x in r) + '\n')

# summary
cnt = collections.Counter(r[6] for r in rows)
print(dict(cnt))
sp = collections.Counter()
for r in rows:
    sp[r[6]] += int(r[20])
print('site-programs by state', dict(sp), sum(sp.values()))
