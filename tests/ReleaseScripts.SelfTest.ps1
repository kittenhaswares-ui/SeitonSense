$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '..\scripts\ReleaseArtifact.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Rejected([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { $null = & $Action } catch { $rejected = $true }
    Assert-True $rejected $Message
}

function New-FixtureZip([string]$Path, [string]$Payload, [int]$Year) {
    $zip = [System.IO.Compression.ZipFile]::Open($Path, 'Create')
    try {
        $entry = $zip.CreateEntry('payload.txt')
        $entry.LastWriteTime = [DateTimeOffset]::new($Year, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $writer = [System.IO.StreamWriter]::new($entry.Open())
        try { $writer.Write($Payload) } finally { $writer.Dispose() }
    }
    finally { $zip.Dispose() }
}

$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$fixtureRoot = Join-Path $temporaryRoot ("SeitonSense-release-test-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    $original = Join-Path $fixtureRoot 'original.zip'
    $repacked = Join-Path $fixtureRoot 'repacked.zip'
    $changed = Join-Path $fixtureRoot 'changed.zip'
    $output = Join-Path $fixtureRoot 'dist'
    New-FixtureZip $original 'same plugin bytes' 2021
    New-FixtureZip $repacked 'same plugin bytes' 2022
    New-FixtureZip $changed 'different plugin bytes' 2022
    Assert-True ((Get-FileHash $original).Hash -ne (Get-FileHash $repacked).Hash) `
        'Fixture ZIP containers must differ even though entry contents match.'

    $first = Save-SeitonVersionedArchive $original $output '1.2.3.4'
    Assert-True (-not $first.Reused) 'The first archive must be created.'
    $originalHash = (Get-FileHash -LiteralPath $first.ArchivePath).Hash
    $originalWriteTime = (Get-Item -LiteralPath $first.ArchivePath).LastWriteTimeUtc
    $checksumPath = "$($first.ArchivePath).sha256"
    $checksum = Get-Content -LiteralPath $checksumPath -Raw

    $second = Save-SeitonVersionedArchive $repacked $output '1.2.3.4'
    Assert-True $second.Reused 'An identical rebuild must reuse its existing version.'
    Assert-True ((Get-FileHash -LiteralPath $second.ArchivePath).Hash -eq $originalHash) `
        'Repacking the same bytes must not change the versioned archive hash.'
    Assert-True ((Get-Item -LiteralPath $second.ArchivePath).LastWriteTimeUtc -eq $originalWriteTime) `
        'Reusing an archive must leave its write time unchanged.'
    Assert-True ((Get-Content -LiteralPath $checksumPath -Raw) -ceq $checksum) `
        'Reusing an archive must preserve its checksum file.'
    Assert-True ((Get-FileHash -LiteralPath (Join-Path $output 'latest.zip')).Hash -eq $originalHash) `
        'The compatibility alias must copy the immutable archive, not the rebuilt container.'

    Assert-Rejected { Save-SeitonVersionedArchive $changed $output '1.2.3.4' } `
        'Changed payload under the same version must be rejected.'
    Assert-True ((Get-FileHash -LiteralPath $first.ArchivePath).Hash -eq $originalHash) `
        'A rejected rebuild must preserve the published bytes.'
    Assert-True ((Get-Content -LiteralPath $checksumPath -Raw) -ceq $checksum) `
        'A rejected rebuild must preserve the published checksum.'
    $next = Save-SeitonVersionedArchive $changed $output '1.2.3.5'
    Assert-True (-not $next.Reused -and $next.Hash -ne $first.Hash) `
        'The changed payload can be packaged after a version bump.'

    $url = Get-SeitonReleaseDownloadUrl '1.2.3.4'
    Assert-True ($url -ceq 'https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/v1.2.3.4/dist/SeitonSense-1.2.3.4.zip') `
        'The feed URL must pin both the version tag and archive filename.'
    $feed = [pscustomobject]@{
        AssemblyVersion = '1.2.3.4'
        DownloadLinkInstall = $url
        DownloadLinkUpdate = $url
        DownloadLinkTesting = $url
    }
    Assert-SeitonReleaseDownloadLinks -Entry $feed
    foreach ($property in @('DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
        $feed.$property = 'https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/main/dist/latest.zip'
        Assert-Rejected { Assert-SeitonReleaseDownloadLinks -Entry $feed } `
            "A mutable artifact URL in $property must be rejected."
        $feed.$property = $url
    }
    foreach ($invalid in @('1.2.3', '1.2.3.4.5', '1.2.3.4-beta', '../1.2.3.4', "1.2.3.4`n", '1.2.3.4;exit')) {
        Assert-Rejected { Get-SeitonReleaseDownloadUrl $invalid } "Invalid version was accepted: $invalid"
    }

    Set-Content -LiteralPath $checksumPath -Value 'corrupt checksum' -Encoding ascii
    Assert-Rejected { Save-SeitonVersionedArchive $repacked $output '1.2.3.4' } `
        'An inconsistent existing checksum must be rejected, never silently replaced.'
    Assert-True ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -eq 'corrupt checksum') `
        'A rejected checksum must remain available for diagnosis.'
    Write-Host 'PASS release scripts: immutable rebuilds, version bumps, pinned URLs, strict versions, checksums.'
}
finally {
    $resolvedFixture = [System.IO.Path]::GetFullPath($fixtureRoot)
    $temporaryPrefix = $temporaryRoot.TrimEnd([char[]]'\/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedFixture.StartsWith($temporaryPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([System.IO.Path]::GetFileName($resolvedFixture)).StartsWith('SeitonSense-release-test-', [StringComparison]::Ordinal)) {
        throw 'Refusing cleanup outside the generated release-test directory.'
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
