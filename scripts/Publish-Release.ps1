param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ReleaseArtifact.ps1')
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path

function Invoke-ReleaseGit([string[]]$Arguments) {
    $result = & git -C $root @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($Arguments -join ' ')" }
    return $result
}

$branch = [string](Invoke-ReleaseGit -Arguments @('branch', '--show-current'))
if ($branch.Trim() -cne 'main') { throw 'Publish releases from main only.' }
$dirty = @(Invoke-ReleaseGit -Arguments @('status', '--porcelain'))
if ($dirty.Count -ne 0) { throw 'Commit all release files before publishing; the working tree must be clean.' }
$remote = [string](Invoke-ReleaseGit -Arguments @('remote', 'get-url', '--push', 'origin'))
if ($remote.Trim() -cnotmatch '\A(?:https://github\.com/|git@github\.com:)kittenhaswares-ui/SeitonSense(?:\.git)?\z') {
    throw 'origin must point to kittenhaswares-ui/SeitonSense.'
}
$headCommit = ([string](Invoke-ReleaseGit -Arguments @('rev-parse', 'HEAD'))).Trim()
$entries = @(Get-Content -LiteralPath (Join-Path $root 'repo.json') -Raw | ConvertFrom-Json)
if ($entries.Count -ne 1) { throw 'repo.json must contain exactly one plugin.' }
$version = [string]$entries[0].AssemblyVersion
$url = Get-SeitonReleaseDownloadUrl $version
$tag = "v$version"
$archive = Join-Path $root "dist\SeitonSense-$version.zip"
& (Join-Path $PSScriptRoot 'Verify-Release.ps1') -ArchivePath $archive -RepositoryRoot $root
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-Content -LiteralPath "$archive.sha256" -Raw).Trim() -cne "$archiveHash  SeitonSense-$version.zip" -or
    (Get-Content -LiteralPath (Join-Path $root 'dist\latest.zip.sha256') -Raw).Trim() -cne $archiveHash -or
    (Get-FileHash -LiteralPath (Join-Path $root 'dist\latest.zip') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $archiveHash) {
    throw 'Versioned checksum or compatibility archive differs from the release.'
}

& git -C $root show-ref --verify --quiet "refs/tags/$tag"
$localTagExists = $LASTEXITCODE -eq 0
if ($localTagExists -and
    ([string](Invoke-ReleaseGit -Arguments @('rev-parse', "$tag^{commit}"))).Trim() -cne $headCommit) {
    throw "Existing local tag $tag points elsewhere; tags are never overwritten."
}
if (-not $Publish) {
    Write-Host "Validated $tag at $headCommit. Run with -Publish to publish its tag, verify the public ZIP, then update main."
    return
}

$remoteTagRows = @(Invoke-ReleaseGit -Arguments @('ls-remote', 'origin', "refs/tags/$tag", "refs/tags/$tag^{}"))
if ($remoteTagRows.Count -gt 0) {
    $peeled = @($remoteTagRows | Where-Object { $_.EndsWith("refs/tags/$tag^{}", [StringComparison]::Ordinal) })
    $remoteCommit = if ($peeled.Count -eq 1) { ($peeled[0] -split '\s+')[0] } else { ($remoteTagRows[0] -split '\s+')[0] }
    if ($remoteCommit -cne $headCommit) { throw "Remote tag $tag already points elsewhere; bump the version." }
}
else {
    if (-not $localTagExists) { $null = Invoke-ReleaseGit -Arguments @('tag', $tag, $headCommit) }
    $null = Invoke-ReleaseGit -Arguments @('push', 'origin', "refs/tags/${tag}:refs/tags/$tag")
}

# The new feed is not pushed until its tag-bound public artifact exists and
# matches the validated local archive. A transient download failure is safe
# to rerun; the existing tag is accepted only if it still names this commit.
$download = [System.IO.Path]::GetTempFileName()
try {
    Invoke-WebRequest -Uri $url -OutFile $download -TimeoutSec 60
    if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -cne $archiveHash) {
        throw 'The public version-tag archive does not match the local release; main was not updated.'
    }
}
finally { Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue }

$null = Invoke-ReleaseGit -Arguments @('push', 'origin', 'HEAD:refs/heads/main')
Write-Host "Published $tag and main with verified archive SHA256 $archiveHash."
