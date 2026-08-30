# Plan — E11.c : la musique en jeu

Date : 2026-08-30. Origine : l'extraction des BGM a été réparée et ré-exportée (`docs/plan-extraction-bgm.md`),
les 46 pistes sont dans le projet — **et rien ne les joue**. E11.a a livré les bruitages ; il reste la musique.

**Tout le §1 est mesuré ou lu dans la décompilation.** Les chiffres de mémoire ont été revérifiés.

## 1. Les faits établis

### 1.1 Ce que la 389 doit jouer

Map 389 → **index musical 25** → `Musics/bgm_025.wav` (121,2 s, 44100 Hz, stéréo, 16 bits,
`loop_detected: true`). L'index vient d'une table de **483 entrées** (`g_defaultSoundOffsetList`),
filtrée par une table d'override conditionnée aux flags de **7 triplets** où **la 389 n'apparaît pas** —
l'override est donc **prouvé inerte** pour elle, exactement comme le groupe de sons d'E11.a.

**Sémantique complète, relue ligne à ligne en session principale après un blocage de relecture** —
ma première rédaction en donnait une fausse. `LoadMapSoundsCore` filtre dans cet ordre :

| valeur | comportement de l'original | site |
|---|---|---|
| `0` | **court-circuit total** : on ne touche pas à la musique | `SoundManager.cs:5168` |
| index **identique** à celui qui joue | **on ne fait rien** — la piste continue | `:5173` |
| `45` | l'ancienne BGM est arrêtée, **aucune séquence n'est chargée** | `:5183` |
| `−1` | **joue l'index 1** — remappé à `LoadMapSequenceCore:532-535` | `:5186` |
| autre | joue cet index | `:5186` |

**Correction de fond : j'avais écrit que `−1` valait « pas de musique ». C'est faux** — les 21 cartes
concernées jouent la piste 1. L'erreur était dans un §1 que je présentais comme lu dans la
décompilation ; elle serait partie dans la table exportée **et dans un test**.

**La garde d'index est ce qui compte le plus ici** : les cartes **389 et 390** partagent l'index 25.
Sans elle, passer de l'une à l'autre **redémarrerait** le thème du navire, ce que l'original ne fait
jamais — et le correctif de propriété du §1.7 rend ce défaut certain plutôt que théorique.

### 1.2 La boucle est propre, et cette fois sans déviation

`loop_detected: true` signifie que l'extracteur a rendu jusqu'au saut arrière : **le fichier se termine
exactement au point de bouclage**. Boucler le tampon entier est donc **fidèle**, sans coupure ni clic —
il n'y a pas d'intro suivie d'une boucle. **Contrairement au son 302 des bruitages**, dont la boucle
démarrait 1820 échantillons après le début : cette déviation-là ne se reproduit pas ici.

### 1.3 Plein volume, immédiatement — et surtout PAS de fondu

`LoadMapSequenceCore` pose `SetSeqVolume(0x7F, 0x7F)`, puis arme une rampe de 10 ticks. **Cette rampe
est mathématiquement un no-op** : à chaque tick le calcul rend `127 − (−12) = 139`, écrêté à `0x7F`.
La piste démarre donc **à plein volume dès le premier tick**.

Le fondu visible à l'entrée de carte est le fondu **d'écran** (`WarpPlayer`), pas un fondu audio.
**Implémenter un fondu d'entrée serait MOINS fidèle, pas plus.**

### 1.4 Quand elle démarre, et le faux second départ

`LoadMapSounds` est l'avant-dernière instruction du bloc d'entrée de carte, juste avant le premier
`Update` — soit la **frame 0** dans la numérotation du harnais.

À la frame suivante, `g_resetSoundFlag` fait ré-émettre `PlaySeq`. **Ce n'est PAS un redémarrage** :
`StartSequencePlayback` ne remet jamais `SeqPosition` à zéro — c'est un ré-armement sans effet audible.

**Décision, après blocage de relecture : cette seconde passe N'EST PAS PORTÉE.** Ma première rédaction
demandait de la porter « idempotente », mais aucun item de la tranche ne créait de seconde passe : le
critère « elle n'en produit pas une seconde » n'aurait rien pu échouer et sa mutation aurait été
**inexécutable**. Ne pas la porter est fidèle *en effet* — elle ne produit aucun son — et supprime la
vacuité au lieu de la contourner.

### 1.5 Rien ne change la musique pendant l'intro

Aucun `0xA5`, `0xA6` ni `0xA7` dans les programmes de la 389. La musique de l'intro est **purement
pilotée par l'entrée de carte**.

### 1.6 Les goldens sont aveugles, une fois de plus

La trace ne porte que deux lignes `SYSTEM` — `LoadMapSounds` en frame 0 et `HandleMapSoundStreaming` en
frame 1 — toutes deux annotées **« not ported »**, sans index, sans identifiant de séquence, sans
volume. Aucun test ne peut assérer la musique depuis le golden : **l'oracle doit être celui de la
tranche**, comme en E11.a.

### 1.7 Un défaut RÉEL dans le code déjà livré (E11.a)

`AlundraSoundPlayer` passe **`owner: this`** (`AlundraSoundPlayer.cs:96`), le moteur compare
`ReferenceEquals(entry.Owner, owner)` (`AudioService.cs:341`), et un lecteur **neuf est construit par
monde** (`AlundraWorldProxy.cs:718`). Donc `World.Clear`, qui appelle `StopVoicesOwnedBy(world)`,
**ne peut jamais arrêter nos voix**.

Des bruitages courts masquent le défaut : ils s'éteignent seuls. **Une musique en boucle de 121 s
jouerait indéfiniment à travers un changement de carte, et se superposerait à la suivante.**
Trouvé par la reconnaissance de cette tranche, vérifié en session principale.

## 2. Les contraintes qui décident de la conception

### 2.1 La table carte → index n'existe que dans la décompilation

Rien sous `data-extracted` ne la porte — vérifié sur les 483 fichiers de carte. **Et ici elle n'est pas
contournable** : sans elle, rien ne sait que la 389 joue la 25. C'est la différence avec le groupe de
sons d'E11.a, qu'on avait pu ignorer parce qu'il était inerte.

### 2.2 Deux routes de lecture, et l'écart est mesuré

| | Route A — `MusicPlayer` (streaming) | Route B — `PlayClip` bouclé |
|---|---|---|
| assets | **46 documents `.sound`** + 46 entrées de catalogue + **ré-export complet** | aucun |
| mémoire | 49 Ko | **20,4 Mio** par piste jouée, jamais libérée (667,9 Mio si les 46) |
| chargement | pas de à-coup | **46 ms** au premier lancement |
| bouclage | streaming | **`AL_LOOPING`, sans coupure** |
| tests | 3 pièces d'infrastructure à porter — dont une qui fait **boucler un test à l'infini** | **le montage d'E11.a, tel quel** |

Le point de test n'est pas rhétorique : `FakeAudioBackend.GetPendingBufferCount` rend `0` en dur, et
`MusicPlayer.FillQueue` boucle tant que le compte est sous 3 — sur un asset bouclé, `read == 0`
n'arrive jamais. **Un test de musique en route A gèlerait la suite.**

## 3. Les décisions

- **D-C-1 — Route B**, `AudioService.PlayClip` sur le bus `Music` avec `IsLooped`. Mesurée : aucun
  plafond de taille, 46 ms de chargement, 20,4 Mio non managés, bouclage sans clic. **Le coût est
  nommé** : la piste reste résidente pour la session (rien n'appelle `Unload`), et une session qui
  visiterait les 46 cartes musicales tiendrait 667,9 Mio. Acceptable ici — l'intro joue **une** piste.
  **Le seam masque le choix** : migrer vers A plus tard sera un changement dans l'implémenteur plus du
  travail convertisseur, sans toucher un seul test.
- **D-C-2 — La table part en données, propriété de l'analyseur, liée par le convertisseur** — le
  précédent exact d'`EntityNames.csv` (« Linked, not copied : l'analyseur possède cette table … une
  seconde copie dériverait »). **Pas de table codée en dur dans la DLL** : 72 % des cartes diffèrent de
  n'importe quelle constante, 35 valeurs distinctes, et ce serait dupliquer de la donnée entre dépôts.
  **Coût assumé et annoncé : un nouvel export complet.** La table est exportée **brute** — `0`, `45`,
  `−1` et les index réels tels quels — et c'est le **consommateur** qui applique la sémantique du §1.1,
  `−1` compris (→ index 1). Exporter des valeurs déjà interprétées y perdrait l'information et
  figerait mon erreur de lecture initiale dans la donnée.
- **D-C-3 — Plein volume, sans fondu** (§1.3). Contre-intuitif, donc écrit dans le code à côté du site.
- **D-C-4 — La garde d'index du §1.1 est portée en entier**, et c'est le vrai contenu de la tranche :
  index `0` ne touche à rien, index **identique** ne redémarre rien, `45` arrête sans charger, `−1`
  joue l'index 1. La seconde passe de la frame 1 **n'est pas portée** (§1.4).
- **D-C-5 — La propriété diffère entre musique et bruitages, et c'est ce qui rend les deux fidèles**
  (refonte après blocage de relecture : ma première rédaction mettait tout en `owner: world`, ce qui
  **coupait la musique** en quittant la 389 — puis la garde d'index empêchait de la relancer sur la
  390. Silence, alors que l'original ne coupe ni ne redémarre. Pire, le critère « aucune nouvelle
  demande » serait resté **vert sur un jeu devenu muet**).
  - **Bruitages : `owner: world`.** C'est le correctif du §1.7, et il est juste pour eux : un bruitage
    n'a aucune raison de survivre à son monde.
  - **Musique : propriété de SESSION.** La voix appartient au directeur de musique, pas au monde, donc
    `World.Clear` ne la touche pas et la piste **continue** à travers un changement de carte de même
    index — exactement ce que fait l'original.
- **D-C-6 — L'état de la garde vit dans un directeur de SESSION, nommé et unique** (blocage P1 de la
  relecture, et c'est le point qui décide de la testabilité). Dans l'original, `g_currentMapSoundIndex`
  est une **globale** : elle survit au changement de carte, par construction. Le porter dans un objet
  reconstruit par monde — comme l'est `AlundraSoundPlayer` (`AlundraWorldProxy.cs:718`) — rendrait la
  garde **vacue par construction** : un directeur neuf n'a rien à garder, la demande repartirait avec
  ou sans garde, et la mutation appariée ne pourrait pas mordre.
  Le plan exige donc : **un directeur de musique de portée session**, qui détient à la fois la voix et
  l'index courant, dont la tranche nomme le site de vie et le moyen de le réinitialiser en test.

## 4. Tranche C1 — la seule à approuver

Portée : `Alundra/`, `Alundra.Tests/`, le convertisseur et l'analyseur pour la seule table du §D-C-2.

1. **La table** : générée côté analyseur, liée côté convertisseur, émise en compagnon de
   `Maps/world-index.json` (même forme, 483 entrées).
2. **`IAlundraMusicPlayer`** — `PlayMapMusic(int mapId)` / `StopMusic()`, déclarée en tête de son
   implémenteur, accrochée à `IEntityWorldContext` en membre par défaut `=> null`, branche dégradée.
3. **`AlundraMusicPlayer`** sur `PlayClip`, bus `Music`, `IsLooped`, plein volume, et **`owner` = le
   directeur de session lui-même, JAMAIS le monde** (D-C-5/D-C-6). *(Correction : cette ligne portait
   encore `owner: world`, resté de la rédaction d'avant D-C-5 — l'exécuteur l'a signalée comme
   contradictoire au lieu de la suivre, et il a eu raison.)*
4. **Le déclenchement** : à l'installation du monde, l'équivalent de `LoadMapSounds` — l'id de carte se
   lit déjà depuis le nom du monde (`-(\d+)$`, mécanisme existant).
5. **D-C-5** : `owner: world` aussi pour `AlundraSoundPlayer`.

**Les tests, écrits AVANT** (§1.6 : ils portent toute la preuve) :

- **T1 — au site de production** : piloter l'entrée de carte produit **exactement UNE** demande, index
  **25**, bouclée, volume plein, bus `Music`.
- **T1 bis — la garde, pilotée sur le DIRECTEUR lui-même** (blocage P1 : le harnais d'intro est
  mono-monde et son faux lecteur *est* la classe de test, donc il ne peut ni entrer sur un second monde
  ni voir la garde du vrai lecteur). Le test pilote donc directement le directeur de session : entrée
  389 → une demande ; entrée **390, même index 25** → **aucune nouvelle demande**, **et la voix est
  toujours vivante et n'a pas été redémarrée**. Cette dernière moitié est indispensable : sans elle, le
  critère serait vert sur un jeu devenu muet (D-C-5).
- **T2 — absence de lecteur** : à l'installation d'un monde sans service audio, **aucune demande et
  aucune exception**. *(Correction de relecture : ma rédaction exigeait « opcode dégradé », repris tel
  quel d'E11.a — mais **il n'existe aucun opcode de musique** ici, le déclenchement est à l'entrée de
  carte et non dans l'interpréteur. Le critère était inexécutable.)*
- **T3 — la résolution, par l'API que le lecteur appelle vraiment** (et non « en donnée pure » : une
  table lue à part laisserait la mutation ci-dessous sans prise). **Au moins une carte dont l'index
  n'est PAS 25**, plus une carte à `0`, une à `45`, une à `−1` → index **1** (§1.1).
- **T3 bis — le compagnon exporté** (blocage de relecture : la moitié convertisseur n'avait aucun
  oracle) : test côté convertisseur assérant que le fichier est écrit à côté de `Maps/world-index.json`,
  qu'il porte **483 entrées**, et que l'entrée **389 vaut 25**. Plus, côté DLL, la **branche dégradée**
  du chargeur — fichier absent ou malformé → aucune demande, aucune exception, sur le modèle de
  `BackdropLoader`.
- **T4 — le lecteur réel** sur un vrai `AudioService` et les faux d'E11.a : **un** clip, `IsLooped`
  vrai, bus `Music`, volume au gain du bus.
- **T5 — la propriété (D-C-5)** : après `StopVoicesOwnedBy(world)`, la voix de musique **et** une voix
  de bruitage sont mortes. **Ce test doit échouer contre le code actuellement livré** — sinon il ne
  teste pas le défaut du §1.7.

**Cinq mutations, chacune vérifiée exécutable avant d'être imposée** :

| mutation | test qui doit tomber |
|---|---|
| ne pas boucler | T4 |
| retirer la garde d'index identique | **T1 bis** (une seconde demande apparaît sur la 390) |
| donner la voix de musique au monde au lieu de la session | **T1 bis** (la voix est morte après le changement) |
| remettre `owner: this` | **T5** |
| coder l'index en dur à 25, au **site de résolution** | **T3** — d'où l'exigence d'une carte dont l'index n'est pas 25 |
| ne pas écrire le compagnon | T3 bis |
| retirer la garde de nullité au site de déclenchement | T2 |

**Acceptation** : suites `Alundra.Tests` et convertisseur au vert ; export complet re-vérifié contre
le baseline **du projet** `D:/development/repo/Alundra Remake/baseline-project-audio-2026-08-30/`
(**1044 fichiers** et **1042 entrées de catalogue**, pris après le ré-export des BGM) — inchangés au
bit près : **les 996 bruitages, leurs manifestes, ET les 46 musiques**, `bgm_025.wav` compris, plus
**les entrées `AssetInfos.json` des deux familles**. *(Blocage de relecture : ma première rédaction ne
protégeait que les bruitages, alors que le ré-export régénère aussi la musique que cette tranche joue
et le registre de guids qui la résout — une altération n'aurait été rattrapée qu'à l'oreille.)* les six goldens inchangés **sauf**
les deux lignes `SYSTEM` du §1.6 dont l'annotation « not ported » devient exacte — **ré-étiquetage à
prouver pur**, méthode d'E7.a et d'E11.a ; **et la validation en jeu par l'utilisateur** : la musique
de la 389 démarre à l'entrée, à plein volume, et boucle sans coupure.

## 5. Budget et arrêts

**Budget** : un commit convertisseur/analyseur, un commit DLL, plus le pointeur ; ≤ 4 tours.

**État de la relecture, à savoir avant d'approuver.** Deux rondes, **dix blocages**, tous traités.
Quatre étaient des erreurs de fond : une **sémantique fausse** (`−1` donné pour « pas de musique »
alors que le code remappe vers l'index 1 — elle serait partie dans la table exportée ET dans un test) ;
la **garde d'index oubliée**, alors qu'elle est le seul comportement audible de la tranche ; une
**contradiction entre deux décisions**, où corriger la propriété des voix aurait rendu le jeu muet à
partir de la seconde carte tout en gardant le critère vert ; et **deux critères vacus** — une seconde
passe qu'aucun item ne créait, et un « opcode dégradé » alors qu'il n'existe aucun opcode de musique.

**Un blocage a été REJETÉ, avec preuve** : la relecture affirmait que les quatre citations de ligne du
§1.1 étaient décalées de +31. Elles sont exactes — les lignes 5168, 5173, 5183 et 5186 contiennent bien
les quatre tests cités, réimprimées une à une en session principale sur un fichier propre vis-à-vis de
git. Les lignes proposées comme « réelles » contiennent du code sans rapport.

**Le plafond de relecture est atteint : la version présente n'a PAS été re-relue.**

**Arrêts** : un fichier audio exporté ou une entrée de catalogue audio qui bouge (bruitages, musiques,
`AssetInfos.json`) ; un golden qui bouge autrement que par les deux
annotations ; toute tentative d'ajouter un fondu d'entrée (§1.3) ou de rejouer la piste à la frame 1
(§1.4) ; et si T5 passe **avant** le correctif, il ne teste pas le défaut du §1.7.
