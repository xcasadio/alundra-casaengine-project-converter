param([string]$Dll, [string]$Out)
# Reads the TypeDef table of an assembly with System.Reflection.Metadata (no dependency loaded).
# One TSV row per type: namespace, name (nested: Outer+Inner), nested(0/1), visibility, generated(0/1)
$fs = [System.IO.File]::Open($Dll, 'Open', 'Read', 'ReadWrite')
$pe = New-Object System.Reflection.PortableExecutable.PEReader($fs)
$md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
$rows = New-Object System.Collections.Generic.List[string]
foreach ($h in $md.TypeDefinitions) {
    $t = $md.GetTypeDefinition($h)
    $name = $md.GetString($t.Name)
    $ns = $md.GetString($t.Namespace)
    $decl = $t.GetDeclaringType()
    $nested = 0
    $full = $name
    if (-not $decl.IsNil) {
        $nested = 1
        $cur = $decl
        while (-not $cur.IsNil) {
            $o = $md.GetTypeDefinition($cur)
            $full = $md.GetString($o.Name) + '+' + $full
            if ($md.GetString($o.Namespace)) { $ns = $md.GetString($o.Namespace) }
            $cur = $o.GetDeclaringType()
        }
    }
    $vis = ($t.Attributes -band [System.Reflection.TypeAttributes]::VisibilityMask).ToString()
    $gen = 0
    if ($name.StartsWith('<') -or $full.Contains('<') -or $name -eq 'EmbeddedAttribute' -or $name -eq 'NullableAttribute' -or $name -eq 'NullableContextAttribute' -or $name -eq 'RefSafetyRulesAttribute' -or $name -eq 'IsExternalInit' -or $name -eq 'NativeIntegerAttribute') { $gen = 1 }
    $rows.Add(($ns + "`t" + $full + "`t" + $nested + "`t" + $vis + "`t" + $gen))
}
$pe.Dispose(); $fs.Dispose()
[System.IO.File]::WriteAllLines($Out, $rows, [System.Text.UTF8Encoding]::new($false))
Write-Output ("{0} types written to {1}" -f $rows.Count, $Out)
