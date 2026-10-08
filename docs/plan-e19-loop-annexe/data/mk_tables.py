import csv
def t(p,k): return {int(r[k]):r for r in csv.DictReader(open(p),delimiter='\t')}
seam=t('seam_b.tsv','idx'); it=t('iters7.tsv','idx'); orc=t('oracle_sha256.tsv','SoundIndex'); lf=t('loops_final.tsv','idx'); tl=t('tails.tsv','idx')
print('| idx | LS | J1 = LoopStartFrame | LoopStartSample | J2 = Frames | P2 / P3 | today Frames | seam step no xf / p99 / max | seam step xf735 (= J1 step) | true step at J2 | it2-end vs it1-end, last frame RMS err / signal | samples until \|true - loop\| > 64 | worst 500 ms window it3..7 vs it2 (dB) |')
print('|---|---|---|---|---|---|---|---|---|---|---|---|---|')
for i in sorted(seam):
    s=seam[i]; l=lf[i]
    g=s['post_first_gt64']; g='never' if g=='-1' else g
    print('| %d | %s | %s | %s | %s | %s / %s | %s | %s / %s / %s | %s | %s | %s / %s | %s | %s |'%(i,l['LS_frame'],s['J1'],l['LoopStartSample'],s['J2'],s['P2'],s['P3'],orc[i]['TodayFrames'],s['step_seam_noxf'],s['step_p99'].replace('.0',''),s['step_max'],s['step_seam_xf735'],s['step_true_at_J2'],s['pre_lastframe_err_rms'],s['lastframe_signal_rms'],g,it[i]['worst_vs_it2']))
print()
print('| idx | end-of-track frame E | today Frames (E+1) | last frame RMS today (cut) | release frames kept | new Frames | new bytes |')
print('|---|---|---|---|---|---|---|')
for i in sorted(tl):
    if i==44: continue
    r=tl[i]; o=orc[i]
    print('| %d | %s | %s | %s | %d | %s | %s |'%(i,r['End'],r['today_Frames'],r['endframe_rms'],int(o['Frames'])-int(r['today_Frames']),o['Frames'],o['Bytes']))
