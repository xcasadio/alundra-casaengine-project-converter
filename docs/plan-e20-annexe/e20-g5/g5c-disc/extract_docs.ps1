param([string]$name)
$src = 'D:\development\repo\alundra-casaengine-project-converter'
$sp = 'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad'
$dst = "$sp\g5c-disc\$name"
$tar = "$sp\g5c-disc\docs_$name.tar"
git -C $src archive --format=tar -o $tar HEAD docs
& "$env:SystemRoot\System32\tar.exe" -xf $tar -C $dst
Remove-Item $tar
"docs extracted" | Out-File -Encoding ascii "$dst\DOCS_DONE.txt"
