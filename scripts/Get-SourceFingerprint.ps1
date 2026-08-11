param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$sourceRoots = @(
    (Join-Path $resolvedRoot 'src\SeitonSense.Core'),
    (Join-Path $resolvedRoot 'src\SeitonSense.Plugin')
)

$lines = foreach ($root in $sourceRoots) {
    Get-ChildItem -LiteralPath $root -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($resolvedRoot, $_.FullName).Replace('\', '/')
            $content = [System.IO.File]::ReadAllText($_.FullName).Replace("`r`n", "`n").Replace("`r", "`n")
            $contentBytes = [System.Text.Encoding]::UTF8.GetBytes($content)
            $fileHash = [Convert]::ToHexString(
                [System.Security.Cryptography.SHA256]::HashData($contentBytes)).ToLowerInvariant()
            "$relative`:$fileHash"
        }
}

$canonical = ($lines | Sort-Object) -join "`n"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($canonical)
$fingerprint = [System.Security.Cryptography.SHA256]::HashData($bytes)
[Convert]::ToHexString($fingerprint).ToLowerInvariant()
