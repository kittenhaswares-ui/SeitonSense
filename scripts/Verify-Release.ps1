param(
    [string]$ArchivePath = (Join-Path $PSScriptRoot '..\dist\latest.zip'),
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$SourceFingerprintPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedArchive = (Resolve-Path -LiteralPath $ArchivePath).Path
$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$repo = @(Get-Content -LiteralPath (Join-Path $resolvedRoot 'repo.json') -Raw | ConvertFrom-Json)
$sourceManifest = Get-Content -LiteralPath (Join-Path $resolvedRoot 'src\SeitonSense.Plugin\SeitonSense.Plugin.json') -Raw | ConvertFrom-Json
$resolvedFingerprintPath = if ([string]::IsNullOrWhiteSpace($SourceFingerprintPath)) {
    Join-Path (Split-Path -Parent $resolvedArchive) 'source.sha256'
} else {
    [System.IO.Path]::GetFullPath($SourceFingerprintPath)
}

if ($repo.Count -ne 1) { throw 'repo.json must contain exactly one plugin.' }
$entry = $repo[0]
if ($entry.InternalName -ne 'SeitonSense.Plugin') { throw 'Unexpected repository InternalName.' }
if ($sourceManifest.InternalName -ne $entry.InternalName) { throw 'Source and repository InternalName differ.' }
if ([int]$entry.DalamudApiLevel -ne 15 -or [int]$sourceManifest.DalamudApiLevel -ne 15) {
    throw 'Dalamud API level must be 15.'
}
$expectedDownloadLink = 'https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/main/dist/latest.zip'
if ($entry.DownloadLinkInstall -ne $expectedDownloadLink -or
    $entry.DownloadLinkUpdate -ne $expectedDownloadLink -or
    $entry.DownloadLinkTesting -ne $expectedDownloadLink) {
    throw 'Install, update, and testing links must all target the canonical main/dist/latest.zip artifact.'
}
if ([bool]$entry.IsHide -or [bool]$entry.IsTestingExclusive) {
    throw 'The public release must be visible and available outside testing mode.'
}
if (-not (Test-Path -LiteralPath $resolvedFingerprintPath -PathType Leaf)) { throw 'Source fingerprint is missing beside the archive.' }

$expectedSourceFingerprint = (Get-Content -LiteralPath $resolvedFingerprintPath -Raw).Trim().ToLowerInvariant()
$actualSourceFingerprint = (& (Join-Path $PSScriptRoot 'Get-SourceFingerprint.ps1') -RepositoryRoot $resolvedRoot).Trim().ToLowerInvariant()
if ($expectedSourceFingerprint -ne $actualSourceFingerprint) { throw 'Published ZIP is stale: source fingerprint changed.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedArchive)
$temporaryDll = [System.IO.Path]::GetTempFileName()
try {
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    $required = @(
        'SeitonSense.Core.dll',
        'SeitonSense.Core.pdb',
        'SeitonSense.Plugin.deps.json',
        'SeitonSense.Plugin.dll',
        'SeitonSense.Plugin.json'
    )
    if ($entryNames.Count -ne $required.Count) {
        throw "Release must contain exactly $($required.Count) files; found $($entryNames.Count)."
    }
    foreach ($name in $required) {
        if ($entryNames -notcontains $name) { throw "Required release entry missing: $name" }
    }

    $unsafePaths = @($entryNames | Where-Object { $_ -match '(^|/)\.\.(/|$)' -or $_ -match '^[/\\]' -or $_ -match '\\' })
    if ($unsafePaths.Count -gt 0) { throw "Unsafe archive path: $($unsafePaths -join ', ')" }

    $forbidden = @($entryNames | Where-Object {
        $_ -match '(^|/)Dalamud\.dll$' -or
        $_ -match '(^|/)FFXIVClientStructs\.dll$' -or
        $_ -match '(^|/)Lumina(\.Excel)?\.dll$'
    })
    if ($forbidden.Count -gt 0) { throw "Framework assemblies must not be bundled: $($forbidden -join ', ')" }

    $manifestEntry = $archive.GetEntry('SeitonSense.Plugin.json')
    if ($null -eq $manifestEntry) { throw 'Packed manifest is missing.' }
    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try { $packedManifest = $reader.ReadToEnd() | ConvertFrom-Json }
    finally { $reader.Dispose() }

    if ($packedManifest.InternalName -ne $entry.InternalName) { throw 'Packed manifest InternalName differs.' }
    if ($packedManifest.AssemblyVersion -ne $entry.AssemblyVersion) { throw 'Packed manifest version differs.' }
    if ([int]$packedManifest.DalamudApiLevel -ne 15) { throw 'Packed manifest API level differs.' }

    $dllEntry = $archive.GetEntry('SeitonSense.Plugin.dll')
    if ($null -eq $dllEntry) { throw 'Packed plugin DLL is missing.' }
    $input = $dllEntry.Open()
    $output = [System.IO.File]::Create($temporaryDll)
    try { $input.CopyTo($output) }
    finally { $output.Dispose(); $input.Dispose() }

    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($temporaryDll).Version.ToString()
    if ($assemblyVersion -ne $entry.AssemblyVersion) {
        throw "DLL version $assemblyVersion differs from repository version $($entry.AssemblyVersion)."
    }
}
finally {
    $archive.Dispose()
    Remove-Item -LiteralPath $temporaryDll -Force -ErrorAction SilentlyContinue
}

$hash = (Get-FileHash -LiteralPath $resolvedArchive -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Seiton Sense release verified: $($entry.AssemblyVersion) / SHA256 $hash"
