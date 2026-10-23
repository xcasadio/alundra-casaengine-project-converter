$ErrorActionPreference = 'Continue'
$src = 'D:\development\repo\alundra-casaengine-project-converter'
$dst = 'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\g5-disc\probe'
New-Item -ItemType Directory -Force -Path $dst | Out-Null
# 1. parent repo: committed content of the DLL and its tests (git archive reads the object database, touches nothing)
$sha = (git -C $src rev-parse HEAD).Trim()
"HEAD at copy time: $sha" | Out-File -Encoding ascii "$dst\COPY_OF_HEAD.txt"
$tar = "$dst\parent.tar"
git -C $src archive --format=tar -o $tar HEAD Alundra Alundra.Tests
tar -xf $tar -C $dst
Remove-Item $tar
# 2. the engine (working tree, without build output and git data)
robocopy "$src\CasaEngineMonogame" "$dst\CasaEngineMonogame" /E /XD bin obj .git .vs TestResults /NFL /NDL /NJH /NJS /NP /R:0 /W:0 | Out-Null
# 3. the export (a real directory, so the post-build copy of Alundra.dll lands in the copy), without the music
robocopy "$src\alundra-project" "$dst\alundra-project" /E /XD Musics /NFL /NDL /NJH /NJS /NP /R:0 /W:0 | Out-Null
"done" | Out-File -Encoding ascii "$dst\COPY_DONE.txt"
