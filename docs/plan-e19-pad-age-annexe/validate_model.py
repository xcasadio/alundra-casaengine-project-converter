import padage_model as pm
# measured by the real proxy in the scratch copy (probe-out/padage-probe-base.txt, padage-probe-fix.txt)
base_old = {(1,0,0):37,(1,1,1):37,(1,17,0):20,(1,18,0):19,(1,26,0):19,(1,26,1):20,(2,0,0):19,(2,2,1):18,(2,18,0):10,(2,26,0):10,(2,26,1):10,(2,28,0):10,(3,3,1):12,(3,0,0):13}
base_new = {(1,0,0):36,(1,1,1):36,(1,17,0):19,(1,18,0):19,(1,26,0):19,(1,26,1):20,(2,0,0):18,(2,2,1):18,(2,18,0):10,(2,26,0):10,(2,26,1):10,(2,28,0):10,(3,3,1):12,(3,0,0):12}
fix_new = {(1,0,0):37,(1,1,1):37,(1,17,0):20,(1,18,0):19,(1,26,0):19,(1,26,1):20,(2,0,0):19,(2,2,1):18,(2,18,0):10,(2,26,0):10,(2,26,1):10,(2,28,0):10,(3,3,1):12,(3,0,0):13}
bad = 0
for k in base_old:
    tpf,a,c = k
    m_b0_old = pm.frames_to_answer('B0', tpf, a, c, False)
    m_b0_new = pm.frames_to_answer('B0', tpf, a, c, True)
    m_b1_new = pm.frames_to_answer('B1', tpf, a, c, True)
    row = (base_old[k]==m_b0_old, base_new[k]==m_b0_new, fix_new[k]==m_b1_new)
    if not all(row): bad += 1
    print(k, 'measured base old/new, fix new =', base_old[k], base_new[k], fix_new[k], '| model B0 old/new, B1 new =', m_b0_old, m_b0_new, m_b1_new, '' if all(row) else '  <-- MISMATCH')
print('mismatches:', bad)
