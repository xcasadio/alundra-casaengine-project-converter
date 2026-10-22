"""trxsum.py - summarise a TRX: failed test names -> first line of the message; optional diff with another TRX."""
import sys, re, collections
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
sys.stdout.reconfigure(encoding="utf-8")


def load(path):
    res = {}
    for r in ET.parse(path).getroot().iter("{%s}UnitTestResult" % NS["t"]):
        name = r.get("testName")
        out = r.get("outcome")
        msg = ""
        m = r.find("t:Output/t:ErrorInfo/t:Message", NS)
        if m is not None and m.text:
            msg = m.text.strip().splitlines()[0][:200]
        res[name] = (out, msg)
    return res


if __name__ == "__main__":
    a = load(sys.argv[1])
    print("tests", len(a), collections.Counter(o for o, _ in a.values()))
    if len(sys.argv) == 2:
        c = collections.Counter(msg[:110] for (o, msg) in a.values() if o == "Failed")
        for k, v in c.most_common(25):
            print(v, k)
    else:
        b = load(sys.argv[2])
        print("other", len(b), collections.Counter(o for o, _ in b.values()))
        newfail = [n for n, (o, m) in b.items() if o == "Failed" and a.get(n, ("?",))[0] == "Passed"]
        fixed = [n for n, (o, m) in b.items() if o == "Passed" and a.get(n, ("?",))[0] == "Failed"]
        print("passed in first, FAILED in second:", len(newfail))
        for n in sorted(newfail):
            print("  ", n, "|", b[n][1])
        print("failed in first, passed in second:", len(fixed))
        for n in sorted(fixed)[:30]:
            print("  ", n)
