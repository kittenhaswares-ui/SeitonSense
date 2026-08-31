param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$rootPrefix = $resolvedRoot.TrimEnd([char[]]@('\', '/')) +
    [System.IO.Path]::DirectorySeparatorChar

function Get-Sha256Hex {
    param([byte[]]$Bytes)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($Bytes)
    }
    finally {
        $sha256.Dispose()
    }

    [System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()
}
$sourceRoots = @(
    (Join-Path $resolvedRoot 'src\SeitonSense.Core'),
    (Join-Path $resolvedRoot 'src\SeitonSense.Plugin')
)

$lines = foreach ($root in $sourceRoots) {
    Get-ChildItem -LiteralPath $root -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object {
            $fullSourcePath = [System.IO.Path]::GetFullPath($_.FullName)
            if (-not $fullSourcePath.StartsWith(
                    $rootPrefix,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Source file escaped its expected repository root: $fullSourcePath"
            }

            $relative = $fullSourcePath.Substring($rootPrefix.Length).Replace('\', '/')
            $content = [System.IO.File]::ReadAllText($_.FullName).Replace("`r`n", "`n").Replace("`r", "`n")
            $contentBytes = [System.Text.Encoding]::UTF8.GetBytes($content)
            $fileHash = Get-Sha256Hex -Bytes $contentBytes
            "$relative`:$fileHash"
        }
}

$canonical = ($lines | Sort-Object) -join "`n"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($canonical)
Get-Sha256Hex -Bytes $bytes
