Get-CimInstance Win32_Process -Filter "Name='python.exe'" | ForEach-Object {
    $c = $_.CommandLine
    if ($c.Length -gt 140) { $c = $c.Substring(0, 140) }
    "{0} {1} MB {2}" -f $_.ProcessId, [int]($_.WorkingSetSize / 1MB), $c
}
