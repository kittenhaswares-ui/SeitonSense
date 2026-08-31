param(
    [string]$CommittedArchive = (Join-Path $PSScriptRoot '..\dist\latest.zip'),
    [string]$BuiltArchive = (Join-Path $PSScriptRoot '..\src\SeitonSense.Plugin\bin\Release\SeitonSense.Plugin\latest.zip')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$committedPath = (Resolve-Path -LiteralPath $CommittedArchive).Path
$builtPath = (Resolve-Path -LiteralPath $BuiltArchive).Path

Add-Type -AssemblyName System.IO.Compression.FileSystem
$committed = [System.IO.Compression.ZipFile]::OpenRead($committedPath)
$built = [System.IO.Compression.ZipFile]::OpenRead($builtPath)
try {
    $committedEntries = @($committed.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
    $builtEntries = @($built.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
    $committedNames = @($committedEntries.FullName | Sort-Object)
    $builtNames = @($builtEntries.FullName | Sort-Object)
    if (($committedNames -join "`n") -ne ($builtNames -join "`n")) {
        throw 'Committed and freshly built package entries differ.'
    }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        foreach ($name in $committedNames) {
            $committedEntry = $committed.GetEntry($name)
            $builtEntry = $built.GetEntry($name)
            if ($null -eq $committedEntry -or $null -eq $builtEntry) {
                throw "Package entry missing during parity check: $name"
            }

            $committedStream = $committedEntry.Open()
            $builtStream = $builtEntry.Open()
            try {
                $committedHash = [System.BitConverter]::ToString(
                    $sha.ComputeHash($committedStream)).Replace('-', '')
                $sha.Initialize()
                $builtHash = [System.BitConverter]::ToString(
                    $sha.ComputeHash($builtStream)).Replace('-', '')
                $sha.Initialize()
            }
            finally {
                $committedStream.Dispose()
                $builtStream.Dispose()
            }

            if ($committedHash -ne $builtHash) {
                throw "Committed package entry differs from the fresh build: $name"
            }
        }
    }
    finally {
        $sha.Dispose()
    }
}
finally {
    $committed.Dispose()
    $built.Dispose()
}

Write-Host 'Committed release entries are byte-identical to a fresh Release build.'
