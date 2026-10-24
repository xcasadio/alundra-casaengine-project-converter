param([string]$name)
$src = 'D:\development\repo\alundra-casaengine-project-converter'
$sp = 'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad'
$dst = "$sp\g5c-disc\$name"
$old = "$sp\g5b-disc\probe"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
$sha = (git -C $src rev-parse HEAD).Trim()
"HEAD at copy time: $sha (committed content only, git archive)" | Out-File -Encoding ascii "$dst\COPY_OF_HEAD.txt"
$tar = "$dst\parent.tar"
git -C $src archive --format=tar -o $tar HEAD Alundra Alundra.Tests docs/plan-e20-annexe docs/plan-oracle-heros.md
& "$env:SystemRoot\System32\tar.exe" -xf $tar -C $dst
Remove-Item $tar
cmd /c mklink /J "$dst\CasaEngineMonogame" "$old\CasaEngineMonogame"
cmd /c mklink /J "$dst\alundra-project" "$old\alundra-project"
"done" | Out-File -Encoding ascii "$dst\COPY_DONE.txt"
