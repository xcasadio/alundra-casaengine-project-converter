"""lib.py (critic h4d2): the EXE image (France) - DATA, BASE, OFF"""
EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE = 0x80020000
OFF = 0x800
