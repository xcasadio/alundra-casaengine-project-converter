import sys
p = sys.argv[1]
raw = open(p, 'rb').read()
t = raw.decode('utf-8-sig')
old = '        font3 = new Font3(Path.Combine(dataFolder, "..", "TAKI\\\\SCREEN"));'
assert t.count(old) == 1, t.count(old)
t = t.replace(old, '        Font3FolderForPrototype = Path.Combine(dataFolder, "..", "TAKI\\\\SCREEN");\r\n' + old)
old2 = '    private static HashSet<string> entitySpriteSheetAlreadySaved = new();'
assert t.count(old2) == 1
t = t.replace(old2, old2 + '\r\n    private static string Font3FolderForPrototype = string.Empty;')
open(p, 'wb').write(b'\xef\xbb\xbf' + t.encode('utf-8'))
print('ok')
