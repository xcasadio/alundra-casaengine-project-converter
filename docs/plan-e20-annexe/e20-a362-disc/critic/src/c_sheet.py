from scen_lib import *
m, STATS = mk(0x23)
log = run(m, 10, lambda t: 0)
sheet = 0x80127008
print('sheet 0x80127008 bytes:', [hex(m.rb(sheet+i)) if hasattr(m,'peek8') else hex(m.rb(sheet+i)) for i in range(0,20)])
rec = m.peek(HERO + 0x1C4)
print('hero +0x1C4 = %#x' % rec)
print('rec bytes:', [hex(m.rb(rec+i)) for i in range(0,20)])
