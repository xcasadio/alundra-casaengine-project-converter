import sys
p='D:/development/repo/alundra-casaengine-project-converter/Alundra/Hero/PlayerManager.cs'
mode=sys.argv[1]
s=open(p,encoding='utf-8',newline='').read()
if mode=='empty':
    a="new System.Collections.Generic.HashSet<int>(Alundra.Ai.Objects.LiftableAi.SpriteTypes);"
    b="new System.Collections.Generic.HashSet<int>();"
elif mode=='stub':
    a="return PlayerTryActionCore(player, in pad, entities, owns, WiredLiftableTypes);"
    b="_ = entities; _ = owns; return 0;"
elif mode=='climb':
    a="if (TryUseItem(player, in pad) == 0 || PlayerTryAction(player, in pad) != 0)\r\n            {\r\n            }\r\n            else if (buttonsHold != 0 && dir != 0 && dir != 0x10)"
    b="if (buttonsHold != 0 && dir != 0 && dir != 0x10)"
assert s.count(a)==1,mode
open(p,'w',encoding='utf-8',newline='').write(s.replace(a,b))
