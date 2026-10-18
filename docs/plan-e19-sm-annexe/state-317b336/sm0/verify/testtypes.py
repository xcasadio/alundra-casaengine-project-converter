"""Independent check: set of type names declared in Alundra.Tests at 9502f75 vs HEAD (read-only, git only)."""
import re
import subprocess
import sys

repo = sys.argv[1]
DECL = re.compile(r"^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|file|ref)\s+)*(?:class|record(?:\s+struct|\s+class)?|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", re.M)


def types_at(rev):
    names = subprocess.run(["git", "-C", repo, "ls-tree", "-r", "--name-only", rev, "--", "Alundra.Tests"], capture_output=True, text=True).stdout.split()
    out = {}
    for path in names:
        if not path.endswith(".cs"):
            continue
        text = subprocess.run(["git", "-C", repo, "show", f"{rev}:{path}"], capture_output=True).stdout.decode("utf-8", "replace")
        for m in DECL.finditer(text):
            out.setdefault(m.group(1), set()).add(path)
    return out


a = types_at("9502f75")
b = types_at("HEAD")
print("only at 9502f75:", sorted(set(a) - set(b)))
print("only at HEAD:", sorted(set(b) - set(a)))
print("counts", len(a), len(b))
