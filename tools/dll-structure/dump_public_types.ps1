param([string]$Dir, [string]$Out)
# Public top-level types of every managed assembly (*.dll) of a folder, from the TypeDef table (nothing is loaded).
# One TSV row per type: assembly file name, namespace, name (generic arity kept).
$rows = New-Object System.Collections.Generic.List[string]
foreach ($f in Get-ChildItem -LiteralPath $Dir -Filter *.dll) {
    try {
        $fs = [System.IO.File]::Open($f.FullName, 'Open', 'Read', 'ReadWrite')
        $pe = New-Object System.Reflection.PortableExecutable.PEReader($fs)
        if (-not $pe.HasMetadata) { $pe.Dispose(); $fs.Dispose(); continue }
        $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        foreach ($h in $md.TypeDefinitions) {
            $t = $md.GetTypeDefinition($h)
            if (-not $t.GetDeclaringType().IsNil) { continue }
            $vis = ($t.Attributes -band [System.Reflection.TypeAttributes]::VisibilityMask)
            if ($vis -ne [System.Reflection.TypeAttributes]::Public) { continue }
            $name = $md.GetString($t.Name)
            if ($name.StartsWith('<')) { continue }
            $rows.Add($f.Name + "`t" + $md.GetString($t.Namespace) + "`t" + $name)
        }
        $pe.Dispose(); $fs.Dispose()
    } catch { }
}
[System.IO.File]::WriteAllLines($Out, $rows, [System.Text.UTF8Encoding]::new($false))
Write-Output ("{0} public top-level types from {1}" -f $rows.Count, $Dir)
