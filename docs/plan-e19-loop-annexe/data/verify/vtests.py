import os, re
T = r"D:\development\repo\alundra-casaengine-project-converter\Alundra.Tests"
claims = {
    ('AlundraMusicPlayerTests.cs', 'PlayCalls'): [211, 215, 216, 221, 235, 262, 266, 274, 304, 314, 323, 332, 333, 378, 379, 380, 384, 453, 490, 494, 495, 533],
    ('AlundraBgmFadeDirectorTests.cs', 'PlayCalls'): [130, 137, 161, 168, 190, 195, 212, 224, 243, 248, 274, 294, 300, 336, 383, 419, 424, 431, 438, 446, 447, 448, 457, 517, 523, 528, 529, 550, 551, 575, 582, 587, 610, 616, 620, 640, 651, 655, 686, 558],
    ('AlundraWarpDepartureTests.cs', 'PlayCalls'): [493, 502, 508, 520, 521, 562, 567, 568, 599, 609, 616, 624, 625, 658, 671, 699, 709, 751, 762, 767, 778, 779, 809, 820, 826, 834, 835],
    ('AlundraBgmFadeDirectorTests.cs', 'CurrentVoiceForTests'): [244, 249, 449],
    ('AlundraMusicPlayerTests.cs', 'CurrentVoiceForTests'): [219, 236, 268, 276, 381, 421],
    ('AlundraWarpDepartureTests.cs', 'CurrentVoiceForTests'): [700, 712],
    ('AlundraBgmFadeDirectorTests.cs', 'IsCurrentVoiceAlive'): [138, 169, 384, 418, 423, 430, 437, 445, 455, 522, 552, 576, 581, 588, 611, 615, 621, 645, 650, 656],
    ('AlundraMusicPlayerTests.cs', 'IsCurrentVoiceAlive'): [275, 312, 315, 324, 334, 420],
    ('AlundraWarpDepartureTests.cs', 'IsCurrentVoiceAlive'): [522, 569, 608, 614, 626, 669, 711, 769, 780, 819, 824, 836],
    ('AlundraBgmFadeDirectorTests.cs', 'REG'): [121, 151, 181, 206, 234, 258, 311, 371, 372, 506, 507, 540, 565, 601, 631],
    ('AlundraMusicPlayerTests.cs', 'REG'): [198, 255, 290, 291, 292, 370, 400, 435, 473, 474, 512],
    ('AlundraWarpDepartureTests.cs', 'REG'): [483, 484, 545, 589, 590, 649, 690, 741, 742, 799, 800],
    ('AlundraBgmFadeDirectorTests.cs', 'GetVoiceBus'): [449],
    ('AlundraMusicPlayerTests.cs', 'GetVoiceBus'): [220, 381, 421],
}
for (fn, pat), lines in claims.items():
    src = open(os.path.join(T, fn), encoding='utf-8').read().splitlines()
    rx = re.compile(r'Register\(.*FakeAudioClip\("bgm') if pat == 'REG' else re.compile(re.escape(pat))
    actual = [i + 1 for i, l in enumerate(src) if rx.search(l)]
    print(fn, pat, 'actual', len(actual), 'claimed', len(lines), 'equal' if sorted(actual) == sorted(lines) else 'DIFF missing=%s extra=%s' % (sorted(set(actual) - set(lines)), sorted(set(lines) - set(actual))))
