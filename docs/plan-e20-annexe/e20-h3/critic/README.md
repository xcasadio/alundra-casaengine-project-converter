# h3-critic : contre-vérification adverse du rapport h3bin

Voir `check.md` (rapport complet, en français). Dossier :

- `run/` : copie du harnais d'oracle de h4 (`lib.py`, `emu.py`, `machine.py`, `world.py`, `bank.py`, `oracle.py`, `spellworld.py`, hitworld/mipsdis/exelib non utilisés) et MON code : `mydis.py`, `myxref.py`, `my_rig.py`, `my_models.py` (mes modèles), `my_diff.py` (vrai code contre modèles), `my_diff_pot.py`, `my_mut.py`, `my_wmut*.py` (mutants du vrai code), `my_goldens.py`, `my_goldcheck*.py`, `my_damage*.py`, `my_maps*.py`, `my_chain_targets.py`, `my_dscan*.py`, `my_scan68.py`, `my_loaddelay.py`, `scen_*.py` (scénarios dans le jeu émulé). Toujours lancer depuis `run/` : `python -B <script>`.
- `lst/` : listings capstone des fonctions relues.
- `results/` : sorties enregistrées (voir les noms ; `goldens_critic.json` = vecteurs autonomes).
