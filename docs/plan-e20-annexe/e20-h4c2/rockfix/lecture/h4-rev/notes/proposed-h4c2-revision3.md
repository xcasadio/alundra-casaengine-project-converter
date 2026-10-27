# Texte proposé : E20.h4c2, révision 3 (D-E20-56, Q-H4C2-3 B ; R-EL-6 après E20.c2b)

Chaque bloc remplace ou complète le passage du plan dont la ligne est donnée (`docs/plan-e20-heros.md` au HEAD `8174c0cb`).

## En-tête (ligne 11443)

`#### 1.31.4 E20.h4c2 — La terre 2 : le pilote `0x800354D0`, E13, l'annulation et le coup de caméra ⏳ (détaillée le 2026-10-18 ; REVISE, REVISE, révisions 1 et 2, **READY** à la relecture de clôture, `ae1fe95` ; **révision 3 du 2026-10-25** : D-E20-56 (Q-H4C2-3, B) et E20.c2b avant h4 (D-E20-83) ; relecture de clôture à refaire)`

## Après l'en-tête : le paragraphe de révision (à insérer avant « Prérequis »)

**Révision 3 (2026-10-25).** La réponse de l'auteur à Q-H4C2-3 (B : le rocher arrêté en l'air attend le sol, D-E20-56) et l'ordre de D-E20-83
(E20.c2b passe avant h0a, donc avant h4) changent trois choses, corrigées ici. (1) E13 ne joue plus son impact (animation 1, son 45, coup de
caméra) tant que le rocher n'est pas au sol : un écart au binaire, consigné (D-H4C2-3). (2) Les clés du rocher de bord de carte et du rocher du
plateau d'octet 16 ou 17 sont remplacées par des clés « corrigées », dérivées du binaire modifié en mémoire (INFÉRENCE, non fidèles à l'octet,
`e20-h4c2/rockfix/`) ; les clés d'origine restent l'oracle de l'original et l'épingle d'(e1). (3) R-EL-6 est reprise : le héros blessé existe
dans la DLL, ses colonnes après T se comparent.

## Prérequis (lignes 11445 à 11446)

**Prérequis.** h0a, h4a ; f5 (D-F5-5) ; f6a pour le seul témoin de (w6) ; la fin du chantier et Q-R-7 ; les défauts retenus de Q-H4B-4,
Q-H4C2-1 (porter le coup) et Q-H4C2-2 (`(E, 13)` pour le seul type 17), et la réponse de l'auteur à Q-H4C2-3 (B : le rocher arrêté en l'air par
un mur ou le bord de la carte n'écrase, n'a son et son coup de caméra qu'au sol, D-E20-56) (révisions 1 et 3). E20.c2b faite avant h0a
(D-E20-83) : elle n'est pas requise par le code de la tranche, elle l'est par R-EL-6 révisée. Ni h4b1, ni h4b2.

## But (ligne 11448 à 11450) : une phrase à ajouter

… chaque rocher qui touche le sol écrase ce qu'il recouvre (type 8, puissance 64), joue son impact et donne un coup à la caméra **à ce moment-là
seulement : un rocher que le bord de la carte ou un mur arrête en l'air continue de tomber et n'agit qu'au sol (écart au binaire, D-E20-56)** ; une
interruption fait retomber au sol les rochers en vol.

## Faits : un puce à insérer après « E13 » (ligne 11460)

- **Un rocher arrêté en l'air** (révision 3 ; FAIT : `disc/dis/events_pass_800386D0.txt`, le statut 3 se prend quand le drapeau `0x10` est levé et
  que `+0x13C` **ou** `+0x144` n'est pas nul ; MESURÉ : `disc/keys/cast_2c_pf.json`, `critic/keys/cast_2c_pf_plateau_hb16.json`).
  - *Dans l'original*, un rocher dont le pas est borné en l'air (le bord de la carte, un mur de plateau) passe au statut 3 au tick suivant et
    E13 joue son impact **tout de suite** : animation cible 1 (vitesse 0, le rocher cesse d'avancer), son 45, coup de caméra, drapeaux
    `0x132044` ; il finit sa chute en animation 1 et meurt 46 ticks après E13. Rocher de bord (héros en (100, 100), id 11) : naît au tick 162 à
    x = 10, statut 3 au 163 à 240 px de haut, sol au 194, `+0xAC` au 209, statut 4 au 210. Rocher 8 du plateau d'octet 16 : bute au 142, statut 3 au
    143 (16 px au-dessus du sol), sol au 146, statut 4 au 190. Un rocher ainsi arrêté est au statut 3 : l'annulation (statut 2 seulement) ne
    l'atteint plus.
  - *Corrigé* (D-E20-56 ; INFÉRENCE, dérivée du binaire modifié en mémoire par `e20-h4c2/rockfix/fixpatch.py` : la première passe d'E13 remet le
    statut à 2 et n'écrit rien d'autre tant que `+0x144 == 0`) : le rocher de bord glisse le long du bord (x = 10, y de 101,23 à 140,44), sol au
    194 (b + 32), statut 3 et coup au 195 (b + 33), `+0xAC` au 241, statut 4 au 242 (b + 80, comme un rocher intérieur) ; le rocher 8 reste collé
    au mur (157,00 ; 72,02), statut 3 et coup au 147 (sol 146), statut 4 au 194 ; les cases libérées plus tard changent l'indice de case des
    rochers suivants (octet 16 : rochers 13, 14 et 15). **Inchangées au bit près** : la clé « milieu de carte » (MD5 `877bb812…`) et les
    interruptions T = 60 et T = 100. Un rocher arrêté en l'air est annulable : à T = 170 et T = 190, le rocher de bord est annulé (un son 45 de
    plus, mort à T + 47 au lieu de 210).
  - *Fréquence* : 1 rocher sur 15 près du bord (3 sur 15 sans le pas par image de la clé de h4) ; dès que le héros est à moins de 120 × 80 px d'un mur
    (critique §5.3).

## Décisions : D-H4C2-1 (fin de la ligne 11480) et deux décisions neuves

- **D-H4C2-1** — … `SpriteTypes = {17}`, `Register` pour `(ProgramEDeactivate, 13)` ; E13 tolère un contexte nul **et n'agit dans sa première
  passe que si le rocher est au sol (D-H4C2-3)**.
- **D-H4C2-3** (D-E20-56) — Le rocher arrêté en l'air attend le sol. Dans la première passe d'E13 (`+0x88 != 1`), si `IsOnGround` est faux :
  `Status = Normal` et rien d'autre (ni animation, ni drapeaux, ni coup) ; la passe des événements du tick suivant le remet au statut 3 tant que
  `ForceAdjusted` est levé, E13 le remet à 2 : **le statut vu à la fin du tick reste 2**, de sorte que `Cancel` (statut 2) l'atteint encore. La
  garde ne porte que sur la première passe : le second branchement (`+0x88 == 1` : détruire à `+0xAC`, l'annulation, le rocher déjà en animation 1)
  n'en a pas. La garde lit `IsOnGround` (le test que la passe des événements fait pour le même drapeau `0x10`), jamais `ForceAdjusted`. Précédent :
  E17 des flèches (`ArrowAi.Deactivate`, `entity.Status = EntityStatus.Normal`). L'écart est consigné dans le commentaire d'E13 et dans l'ADR de la
  tranche ; il ne vaut que pour le type 17 (E3, donc la terre 1 et les boules du feu 1, gardent l'original : Q-H4C2-6).
- **D-H4C2-4** — Les clés de l'écart : `cast_2c_pf_rockfix`, `cast_2c_pf_plateau_hb16_rockfix`, `cast_2c_pf_plateau_hb17_rockfix` et
  `cancel_2c_pf_t170_rockfix` (T0 (a)), dérivées du binaire modifié en mémoire, deux exécutions au manifeste identique ; elles sont marquées
  « indicatives, non fidèles à l'octet » dans le `README` de l'annexe. Les clés d'origine restent l'oracle de l'original.

## Périmètre (lignes 11490 à 11493)

Inchangé, avec : l'annexe `docs/plan-e20-annexe/e20-h4c2/` reçoit `rockfix/` (le patch, les quatre scripts, les clés, le manifeste) ; **Hors
périmètre** : … la blessure du héros (E20.c2b, faite avant, D-E20-83) ; E3 pour tous les types (Q-H4C2-6).

## T0 (lignes 11497 à 11506)

(a) … ajouter : les clés « corrigées » de `rockfix/` (deux exécutions au manifeste identique ; `fixpatch.py` ne change qu'E13) et la preuve que
`cast_2c_pf_mid_x600y480` et les interruptions T = 60 et T = 100 sont identiques avec et sans le patch.
(c) sondes, à ajouter : (s9) un rocher dont le pas est borné en l'air (bord, mur) n'a ni statut 3 vu à la fin d'un tick, ni son 45, ni coup avant
le sol ; statut 3 et coup au tick du sol + 1 ; (s10) la position finale du rocher de bord (x = 10, y de la clé corrigée) et du rocher 8 (arrêt net
contre le mur) : le glissement oblique de la DLL après le contact (« la division conjointe en diagonale », D-E20-80) est mesuré et consigné en
R-EL-5 étendue à la position ; (s11) E20.c2b présente : un `0x31` posé est géré (pas par le défaut de h0a), la garde de h4a l'annule, le son 23 du
héros est joué ; (s12) la case de la carte 14 sous le héros à (100, 100) n'est pas dangereuse après c2b (`n = (+0x180 & 0x180) >> 7 = 0`).

## T1 (lignes 11507 à 11528)

- (e1) E13 contre les 40 vecteurs de `vec_e13` : les 20 vecteurs de première passe se rejouent **au sol** (l'adaptateur pose `IsOnGround` : aucun
  vecteur ne lit `+0x144`, E13 d'origine ne le lit pas) ; les 20 autres tels quels.
- (e6) première passe en l'air : le statut remis à 2, `TargetAnimationId`, drapeaux et `[0x800DD874]` inchangés ; (e7) au sol avec `ForceAdjusted`
  levé : l'impact a lieu (la garde lit `IsOnGround`).
- (w2) le rocher de bord contre `cast_2c_pf_rockfix`, accolé à un rocher intérieur (statut 3 et coup au tick 195, position finale de la clé).
- (w4) les interruptions `cancel_2c_pf_t100`, `_t60` et **`_t170_rockfix`** (le rocher de bord est en l'air : annulé, un son 45 de plus) ; le héros
  après T et son son 23 se comparent (R-EL-6 révisée).
- (w8) le rocher 8 contre `cast_2c_pf_plateau_hb16_rockfix` : pas de coup au 143, statut 3 et coup au 147 (sol 146), statut 4 au 194 ; R-EL-5 ne
  joue plus sur le tick du statut 3 (il ne dépend plus du drapeau de blocage) mais sur la position (s10).

## T3 : quatre mutants de plus (au moins 32)

E13 sans la garde du sol (l'original ; tué par (e6), (w2), (w8)) ; garde lue sur `ForceAdjusted` au lieu d'`IsOnGround` (tué par (e7), (w2)) ;
statut laissé à 3 en l'air (tué par (w4) à T = 170 et par (e6)) ; garde appliquée aussi au second branchement (tué par (e3), (e4), (w4)).

## Acceptation, preuve de retrait (lignes 11546 à 11549)

Ajouter : neutraliser la garde du sol d'E13 rend rouges exactement (e6), (w2), (w8) et (w4) à T = 170 ; (e1), (e7), (w1), (p1) et (p4) restent
verts (aucun rocher du banc milieu de carte ni des interruptions T = 60 et 100 ne bute).
**Recette de l'auteur** : ajouter : Alundra à moins de 120 px du bord ouest de l'arène (carte 14) : un rocher poussé contre le bord n'écrase et ne
fait son bruit et sa secousse qu'au sol.

## Arrêts (ligne 11558)

… une réponse de l'auteur autre que le défaut à Q-H4B-4, Q-H4C2-1 ou Q-H4C2-2, ou autre que B à Q-H4C2-3, ou autre que le défaut à Q-H4C2-5 ou
Q-H4C2-6 (révisions 1 et 3) ; une sonde (s9) à (s12) contredite.
