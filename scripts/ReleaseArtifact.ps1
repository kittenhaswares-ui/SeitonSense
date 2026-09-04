Set-StrictMode -Version Latest

function Get-SeitonReleaseDownloadUrl {
    param([Parameter(Mandatory)][string]$Version)

    if ($Version -cnotmatch '\A[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+\z') {
        throw 'Release version must contain exactly four numeric parts.'
    }
    return "https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/v$Version/dist/SeitonSense-$Version.zip"
}

function Assert-SeitonReleaseDownloadLinks {
    param([Parameter(Mandatory)]$Entry)

    $expected = Get-SeitonReleaseDownloadUrl -Version ([string]$Entry.AssemblyVersion)
    if ($Entry.DownloadLinkInstall -cne $expected -or
        $Entry.DownloadLinkUpdate -cne $expected -or
        $Entry.DownloadLinkTesting -cne $expected) {
        throw 'Install, update, and testing links must all target this exact versioned archive on its version tag.'
    }
}

function Save-SeitonVersionedArchive {
    param(
        [Parameter(Mandatory)][string]$BuiltArchive,
        [Parameter(Mandatory)][string]$OutputDirectory,
        [Parameter(Mandatory)][string]$Version
    )

    $null = Get-SeitonReleaseDownloadUrl -Version $Version
    $builtPath = (Resolve-Path -LiteralPath $BuiltArchive).Path
    $outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    $releaseName = "SeitonSense-$Version.zip"
    $releasePath = Join-Path $outputPath $releaseName
    $hashPath = "$releasePath.sha256"
    $reused = Test-Path -LiteralPath $releasePath
    if ($reused) {
        try {
            & (Join-Path $PSScriptRoot 'Verify-BuiltPackageParity.ps1') `
                -CommittedArchive $releasePath -BuiltArchive $builtPath
        }
        catch {
            throw "Version $Version already has a different archive payload. Bump the version before packaging. $($_.Exception.Message)"
        }
    }
    else {
        # File.Copy's no-overwrite overload also rejects a competing creator
        # between the existence check and this write.
        [System.IO.File]::Copy($builtPath, $releasePath, $false)
    }

    $hash = (Get-FileHash -LiteralPath $releasePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedHashLine = "$hash  $releaseName"
    if (Test-Path -LiteralPath $hashPath) {
        if ((Get-Content -LiteralPath $hashPath -Raw).Trim() -cne $expectedHashLine) {
            throw "Existing versioned checksum differs from $releaseName; refusing to replace it."
        }
    }
    else {
        Set-Content -LiteralPath $hashPath -Value $expectedHashLine -Encoding ascii
    }

    # Compatibility alias follows the immutable archive, never a timestamp-
    # different rebuild of the same payload.
    Copy-Item -LiteralPath $releasePath -Destination (Join-Path $outputPath 'latest.zip') -Force
    Set-Content -LiteralPath (Join-Path $outputPath 'latest.zip.sha256') -Value $hash -Encoding ascii
    return [pscustomobject]@{ ArchivePath = $releasePath; Hash = $hash; Reused = $reused }
}
