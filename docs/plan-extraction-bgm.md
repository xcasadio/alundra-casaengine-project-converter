# Plan — Remettre en état l'extraction des musiques

Date : 2026-08-30. Origine : E11.a a livré les bruitages de l'intro, et l'utilisateur a demandé
« mais il reste le problème de l'export des musiques ? ». Oui : **26 des 46 pistes sont 5 secondes de
silence**, dont l'index 25 dont la 389 a besoin.

**Le travail se fait dans le sous-module `alundra-datas-analyser`**, pas dans ce dépôt. Le plan vit ici
parce que c'est ici que se lisent les plans du chantier, et parce que c'est ce dépôt qui consomme le
résultat.

## 1. Les faits établis — mesurés, pas supposés

### 1.1 Le plantage, reproduit et chiffré

En rendant la piste 19, à la **frame 1320** :

```
note = 38   centre de tonalité = 100   fine = 77
noteDelta = (38 + 0x3C) − 100 = −2
index = (−2 << 4) + 9 = −23        table de 192 entrées   →  IndexOutOfRangeException
```

Site : `AlundraTools/AlundraEngine/Sound/SoundManager.cs:4255`, dans `FUN_80091b60`, atteint par le
chemin note-on du séquenceur. **La piste 19 casse seule** — aucune accumulation nécessaire.

### 1.2 Ce n'est PAS un défaut de portage — c'est la donnée du jeu

La fonction est le **`note2pitch` de la libsnd de Sony**. Vérifié en session principale contre les deux
décompilations de référence présentes sur la machine
(`psyz/decomp/src/libsnd/vm_n2p.c`, `decomp/sotn-decomp-master/src/main/psxsdk/libsnd/vmanager.c`) :

- la table de hauteurs est **identique sur ses 192 entrées** ;
- le canonique fait `octave = semitones / 12` puis `semitone = semitones − octave*12` — il **tronque**,
  exactement comme le C# ;
- l'écart d'adresse `note2pitch` → `note2pitch2` vaut 0xB8 des deux côtés : même build de libsnd.

**La note de reconnaissance affirmant que « le MIPS d'origine utilise un modulo plancher » est
RÉFUTÉE.** `div` tronque. Il n'y a **aucune arithmétique à corriger**.

La cause est la donnée : le biais `+60` de l'algorithme couvre les notes jusqu'à **cinq octaves** sous
le centre. Or la piste 19 porte un instrument **en couches** dont la **tonalité 3 est un tapis pleine
étendue** (`min 0, max 127`) centré sur **100**. La note 38 est donc **62 demi-tons sous le centre —
deux de trop**. La tonalité 0 de la même note joue correctement ; c'est la seconde itération de la
boucle de couches qui déborde.

### 1.3 Pourquoi la vraie console ne plante pas — et pourquoi on ne peut pas la reproduire

Le C n'a pas de contrôle de bornes : la PSX lit tranquillement le demi-mot **avant** la table et
continue. **La différence n'est pas l'algorithme, c'est le langage.**

Et ce qu'elle lit tombe dans l'**état runtime de la libsnd** — de la mémoire mutable, pas une
constante. **Le comportement d'origine n'est donc pas reproductible, même en principe.** Aucun
émulateur ni désassembleur ne changerait cela : c'est une propriété du code, pas une lacune d'outillage.

### 1.4 Le rayon de souffle, mesuré

La boucle d'extraction **attrape l'exception et continue** (`AlundraDataExtractor/Program.cs:180-184`),
mais le système sonore reste figé au milieu d'une note-on. Profil observé, et il colle exactement :

| index | état | cause |
|---|---|---|
| 1–18 | audibles | avant le plantage |
| **19** | **absente du manifeste** | l'exception, avalée |
| 20 | tronquée (6,5 s au lieu de ~2 min) | état déjà dégradé |
| 21–46 | **silence** (300 frames, crête 1) | système sonore empoisonné |

**Mesure décisive : en sautant la 19, les 45 autres rendent du son**, dont la **25** (crête 22271).
Donc l'empoisonnement vient de **l'exception**, pas d'une accumulation.

### 1.5 Le vrai scandale : rien n'a prévenu

26 fichiers de silence pur ont été écrits, `report.json` a affiché `"Audio.Bgm": 45` et **zéro
avertissement**. Pire : la donnée qui permettait de le détecter était **déjà dans le manifeste** —
`first_audible_frame: -1` sur les 26. Elle n'a jamais été relue. C'est ce qui a permis au défaut de
dormir jusqu'à ce qu'E11 aille chercher la piste 25.

### 1.6 Deux mines armées ailleurs, et une anomalie distincte

- **Le même débordement existe dans deux autres copies** : `CalculateVoicePitch`
  (`SoundManager.cs:4774`, le chemin **SFX**) et `CalculateToneRawPitch` (`SoundBin.cs:1929`,
  **l'exportateur**). **274 tonalités SFX** peuvent produire un delta négatif ; aucune n'a encore été
  déclenchée assez bas. Le chemin SFX n'est pas correct — il est **chanceux**.
- **L'index 44 est muet même seul**, aux deux durées de rendu, alors qu'il sonnait dans un premier
  passage court sur les 46. Il dépend donc de quelque chose qu'une piste antérieure installe.
  **Anomalie distincte du plantage de la 19, non élucidée.**
- Divergence mineure trouvée en vérifiant : la table d'Alundra a **192** entrées, celle de libsnd
  **196** (`0x2000` puis trois zéros). L'index maximal atteignable est 191 → **sans effet**.

## 2. Les contraintes qui décident de la conception

### 2.1 Il n'existe aucun projet de test pour l'extracteur ni pour le moteur décompilé

Le sous-module ne teste que MGUI. La discipline habituelle du chantier — « un test traversant le site
d'appel de production, prouvé par neutralisation » — **ne se transpose pas telle quelle**.

**L'oracle de remplacement est plus fort que d'habitude, et il faut s'en servir : l'IDENTITÉ OCTET À
OCTET des sorties actuellement correctes.** Les pistes **1 à 18** rendent aujourd'hui du son ; toute
modification qui les déplace est un défaut. Idem pour **les 996 WAV de bruitages et
`data-extracted/sound/sfx.json`**, que la garde du §3 touche potentiellement. C'est un oracle massif,
gratuit, et discriminant.

**Mais il ne suffit pas, et il faut le dire ici plutôt que le découvrir à la recette** (blocage de
relecture) : il ne couvre **par construction pas** les 27 pistes dont les octets DOIVENT changer, et le
critère « audible » du code est une simple crête à 64 — du bruit le franchit. L'oracle complet est donc
en trois couches : identité octet à octet là où rien ne doit bouger, propriétés chiffrées
(`LoopDetected`, `Frames` bien au-delà du seuil de grâce) là où ça doit changer, et **l'oreille de
l'utilisateur** en dernier ressort sur les deux pistes qui comptent.

### 2.2 Le moteur décompilé est l'autorité de fidélité

Toute modification doit être **inerte partout où ça marche aujourd'hui** — et la garde proposée l'est
par construction : elle ne s'applique que là où le code lève actuellement une exception.

### 2.3 Le comportement d'origine est irrécupérable

Corollaire à écrire dans le code, pas seulement dans ce plan : **toute garde est une déviation**. On ne
choisit pas « la bonne valeur », on choisit la moins mensongère et la plus facile à changer.

## 3. Les décisions

- **D-X-1 — La garde SAUTE la voix ; ni plancher, ni écrêtage.** Le modulo plancher donnerait la
  hauteur musicalement juste (117, contre 117,85 attendu) — mais ce serait une **amélioration
  délibérée** : rendre audible une couche que la PSX n'a jamais jouée correctement. L'écrêtage est
  arbitraire. **Sauter la voix n'ajoute rien qui n'existait pas** et reste déterministe. La piste 19
  rendra donc avec une couche manquante, ce qui est un état **connu et documenté**, pas une réparation.
- **D-X-2 — Un seul point de politique.** La garde vit dans **une seule méthode nommée**, appelée par
  les trois sites, pour que passer au plancher soit un changement d'**une ligne** le jour où tu
  voudrais trancher à l'oreille sous émulateur.
- **D-X-3 — Les trois sites sont gardés**, pas seulement celui qui casse. Le chemin SFX est chanceux,
  pas correct, et l'exportateur planterait de la même façon sur une donnée un peu différente.
  **La politique du site EXPORTATEUR est différente et doit être écrite** (blocage de relecture) :
  `CalculateToneRawPitch` est une fonction pure dont le retour devient le **taux d'échantillonnage du
  WAV exporté** — « sauter la voix » n'y veut rien dire. Elle rend donc une **sentinelle
  « incalculable »**, et son appelant écarte la tonalité.
  **Correction de relecture, et elle est structurante : l'enregistrement du refus NE DOIT PAS changer le
  schéma de `sfx.json` quand il ne se déclenche pas.** Il n'existe aujourd'hui **aucun canal de refus au
  niveau tonalité** — `SfxToneExport` n'a pas de champ dédié et l'écart existant est un `continue`
  silencieux (`SoundBin.cs:412-415`). En ajouter un émettrait `"SkipReason": null` sur **chaque**
  tonalité, puisque le sérialiseur n'omet pas les nuls (`Program.cs:17`) : un correctif juste ferait
  alors bouger `sfx.json` et déclencherait un arrêt écrit — **la même classe de contradiction que le
  défaut de la piste 20**. On réutilise donc le champ **existant au niveau ENREGISTREMENT**,
  `SfxExportRecord.SkipReason`, renseigné **uniquement en cas de déclenchement réel**.
  *(Et l'analogie de la première rédaction était fausse : les 91 écartées ne sont pas des refus de
  tonalité mais des enregistrements à `NumTones = 0` portant déjà un `SkipReason`.)*
- **D-X-6 — L'inertie se MESURE, elle ne se suppose pas** (blocage de relecture). La méthode de garde
  unique **compte ses déclenchements par site**, et le run les rapporte. Sans ce compteur, « les deux
  autres gardes sont inertes » est une hypothèse invérifiable, et l'identité octet à octet des SFX la
  satisferait **vacuement**. Même leçon que la preuve positive d'exécution des goldens : ne jamais
  accepter « ça n'a rien changé » sans montrer que le chemin a bien été traversé.
- **D-X-7 — Un refus d'écrire doit AUSSI retirer le fichier périmé** (blocage de relecture). La boucle
  ne nettoie pas son dossier de sortie (`Program.cs:150`) : refuser d'écrire une piste muette laisserait
  **le fichier de silence précédent en place**, et « un rendu silencieux devient un échec, pas un
  fichier » serait faux sur le disque. La tranche supprime donc le fichier périmé.
- **D-X-4 — Un rendu silencieux devient un ÉCHEC, pas un fichier.** C'est le correctif le plus
  important du lot : il transforme toute panne future d'invisible en bruyante. La donnée existe déjà
  (`first_audible_frame`), il suffit de la relire et de refuser d'écrire.
- **D-X-5 — L'isolation par piste est une tranche SÉPARÉE et subordonnée à l'identité octet à octet.**
  Elle n'est plus nécessaire une fois l'exception supprimée (§1.4), mais elle supprimerait une classe
  entière de dépendance entre pistes — ce que l'anomalie de l'index 44 laisse justement soupçonner.
  Si elle déplace ne serait-ce qu'un octet des pistes 1–18, **on l'abandonne** : la robustesse ne vaut
  pas une régression sur des données correctes.

## 4. Tranches

### X1 — La garde, l'échec bruyant, et l'oracle *(la seule à approuver maintenant)*

Portée : `alundra-datas-analyser` seul — `AlundraEngine/Sound/SoundManager.cs`,
`AlundraEngine/Sound/SoundBin.cs`, `AlundraDataExtractor/Program.cs`.

0. **LE BASELINE, AVANT TOUT LE RESTE — sans lui, l'oracle principal est détruit par sa propre
   recette** (blocage de relecture, P1). `data-extracted/` est **dans le `.gitignore`** : la seule copie
   de référence est le dossier de travail que l'extraction **réécrit sur place**, et il n'existe **aucun
   mode d'extraction « son seul »** — produire `sfx.json` impose le run complet, qui réécrit tout
   l'arbre. Lancer l'extraction d'après-correctif en premier rendrait **toutes** les comparaisons
   d'identité et **tous** les arrêts du §5 invérifiables, et perdrait la garantie que l'audio déjà livré
   dans le dépôt parent n'a pas bougé.
   **Forme retenue : des EMPREINTES, pas une copie.** Un `sha256` par fichier (1043 lignes, ~500 Ko)
   plus une copie intégrale des deux petits JSON, au lieu de dupliquer **388 Mo** — 700 fois moins cher
   et strictement suffisant pour « est-ce que quelque chose a bougé ». *(Un baseline de ce type a déjà
   été pris en session principale par précaution ; la tranche le regénère dans un emplacement durable
   et le nomme ici.)* **Toutes les comparaisons d'identité du présent §4 et tous les arrêts du §5
   s'évaluent contre ce baseline.**
1. **Le mode de vérification** — un mode CLI `--verify-bgm <soundBin>` qui rend les
   46 index et rapporte, par piste, la crête et le verdict audible/muet. Il existe déjà en prototype
   dans mon banc d'essai ; il devient un mode de première classe, à côté de `--render-bgm` et
   `--trace-bgm` qui suivent déjà cette forme. **Lancé AVANT tout correctif, il doit reproduire le
   profil du §1.4** — 1–18 audibles, plantage sur 19. S'il ne le reproduit pas, il ne mesure pas le
   défaut : arrêt.
2. **La garde** (D-X-1/2/3) : une méthode nommée unique, appelée par `FUN_80091b60`,
   `CalculateVoicePitch` et `CalculateToneRawPitch`, qui signale « index hors table » ; les appelants
   sautent la voix. Commentaire obligatoire au point de politique : l'original lisait hors bornes, la
   valeur lue est de l'état runtime mutable, **elle n'est pas reproductible**, et le plancher est
   l'alternative documentée à une ligne.
3. **L'échec bruyant** (D-X-4, D-X-7) : un rendu sans aucune frame audible n'est plus écrit comme
   fichier ni compté comme succès — il est signalé, **le fichier périmé est supprimé**, et le compte
   final imprime un triplet **rendues / muettes / échouées**.
4. **Le compteur de gardes** (D-X-6) : le run rapporte, par site, le nombre de déclenchements.

**Acceptation.** L'identité octet à octet est l'oracle principal **là où elle s'applique** — et la
relecture a montré qu'elle ne s'applique ni partout ni seule.

**Ce qui doit rester identique, octet pour octet — comparé au baseline de l'étape 0 :**

- **BGM 1 à 18 seulement.** *(Correction de relecture : le plan mettait aussi la 20 dans cet ensemble,
  alors que son §1.4 dit qu'elle est tronquée PAR l'état dégradé. Un correctif juste DOIT donc la
  changer — le critère déclarait un arrêt obligatoire sur une implémentation correcte.)*
- **Les 996 WAV de bruitages et `data-extracted/sound/sfx.json`.** *(Correction de relecture : le plan
  écrivait `sfx-manifest.json`, qui est le fichier du CONVERTISSEUR, pas de l'extracteur. Le chemin
  n'existe pas ici — la vérification aurait comparé du vide et serait passée en silence.)*

**Ce qui doit changer, et comment :**

- **20** : rend sa vraie durée, `LoopDetected: true`, `first_audible_frame >= 0`.
- **19** : rend, avec une couche manquante — état connu (D-X-1), pas une réparation.
- **21 à 46, SAUF 44** : audibles, `first_audible_frame >= 0`, **`LoopDetected: true`**, et
  **`Frames` très supérieur au seuil de grâce de 300** — sans quoi une piste tronquée passerait.
- **44** : reste muette, et c'est sa seule disposition attendue dans cette tranche (§1.6, tranche X3).
  Elle doit donc être **absente de `bgm.json` ET absente du dossier de sortie** (D-X-7).

**Ce que l'automatique NE PEUT PAS prouver, et qui exige ton oreille** *(blocage de relecture)* : le
seuil d'audibilité est une crête à 64 — **du bruit, de mauvais instruments ou de mauvaises hauteurs le
franchissent exactement comme de la vraie musique**, et l'identité octet à octet ne couvre par
construction pas les pistes qui doivent changer. X1 pourrait donc livrer 26 fichiers non muets et
faux, index 25 compris. **Écoute obligatoire de `bgm_025.wav` (la piste de la 389) et de
`bgm_019.wav` avant de déclarer la tranche faite.**

**Rapport de gardes attendu** (D-X-6) : déclenchements > 0 sur le site séquenceur (piste 19),
**exactement 0** sur le site SFX et sur le site exportateur.

**Deux mutations obligatoires, chacune vérifiée exécutable avant d'être imposée :**

| mutation | ce qui doit tomber |
|---|---|
| retirer la garde | la piste 19 relève, le profil du §1.4 revient |
| retirer l'échec bruyant (D-X-4) | **`bgm_044.wav` réapparaît dans `bgm.json`** et le triplet final annonce 46 rendues |

*(La seconde mutation était **vacue** dans la première rédaction : aucun artefact vérifié ne bougeait
en la retirant. C'est la troisième fois que ce piège se présente sur ce chantier ; il est désormais
attaché à deux artefacts nommés.)*

**Ce que cette tranche ne fait PAS** : elle ne touche pas au convertisseur, ne relance pas l'export du
projet, et ne branche pas la musique dans le jeu.

### X2 — Isolation par piste *(à approuver plus tard, subordonnée à X1)*

Réinitialiser le système sonore entre deux pistes. **Abandonnée si elle déplace un seul octet des
pistes 1–18** (D-X-5).

### X3 — L'anomalie de l'index 44 *(investigation séparée)*

Muette même seule ; audible seulement dans certaines séquences. À diagnostiquer avec la même méthode
que la 19 — mesure d'abord, hypothèse ensuite.

### X4 — Aval : ré-export et musique en jeu *(hors de ce plan)*

Une fois les WAV corrects : relancer l'**export complet** du convertisseur (règle permanente du
chantier), puis **E11.c** côté DLL — asset `.sound` en streaming, `MusicPlayer.Play` à l'entrée de
carte, index **25** pour la 389.

## 5. Conséquences de dépôt, budget, arrêts

**Le commit vit dans le sous-module**, donc le pointeur du dépôt parent bouge : les deux commits sont à
faire, et dans cet ordre. Attention au piège déjà rencontré sur ce chantier — un `cd` dans un
sous-module a déjà fait atterrir des commits dans le mauvais dépôt : **chemins absolus ou
`git -C <racine>`**.

**Budget** : X1 en un commit de sous-module plus le pointeur, ≤ 3 tours.

**État de la relecture, à savoir avant d'approuver.** Ce plan a été relu **deux fois** et a rendu
**REVISE** deux fois : sept blocages réels au total, tous corrigés, dont **quatre erreurs de fond**
— un critère qui déclarait un arrêt sur une implémentation correcte (piste 20), un fichier de
comparaison qui n'existe pas (`sfx-manifest.json` au lieu de `sound/sfx.json`), une mutation vacue
doublée d'un trou réel (le dossier de sortie n'est jamais nettoyé), et un oracle que sa propre recette
détruisait (aucun baseline préservé). **Le plafond de relecture est atteint : la version présente n'a
PAS été re-relue.** Les deux dernières corrections sont textuelles et locales — un ajout d'étape et une
correction de politique — mais l'utilisateur approuve en le sachant.

**Arrêts** : une BGM de 1 à 18, un des 996 WAV de bruitages ou `sound/sfx.json` qui bouge ; un
déclenchement de garde sur le site SFX ou exportateur ; toute modification hors des trois fichiers nommés ;
toute tentative de « réparer » la piste 19 en prétendant retrouver le comportement d'origine (§1.3) ;
et si `--verify-bgm` ne reproduit pas le profil du §1.4 avant correctif, il ne mesure pas le défaut.
