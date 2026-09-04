param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ReleaseArtifact.ps1')

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$solution = Join-Path $projectRoot 'SeitonSense.slnx'
$testProject = Join-Path $projectRoot 'tests\SeitonSense.Core.SelfTest\SeitonSense.Core.SelfTest.csproj'
$pluginTestAssembly = Join-Path $projectRoot 'tests\SeitonSense.Plugin.SelfTest\bin\Release\SeitonSense.Plugin.SelfTest.dll'
$sourceZip = Join-Path $projectRoot 'src\SeitonSense.Plugin\bin\Release\SeitonSense.Plugin\latest.zip'
$repo = @(Get-Content -LiteralPath (Join-Path $projectRoot 'repo.json') -Raw | ConvertFrom-Json)
if ($repo.Count -ne 1) { throw 'repo.json must contain exactly one plugin.' }
$version = [string]$repo[0].AssemblyVersion
Assert-SeitonReleaseDownloadLinks -Entry $repo[0]
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\SeitonSense.Plugin\SeitonSense.Plugin.csproj') -Raw)
$projectVersion = [string]$projectXml.Project.PropertyGroup.Version
if ($version -ne $projectVersion) { throw "repo.json version $version differs from project version $projectVersion." }

New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

& (Join-Path $PSScriptRoot 'Verify-SafetyContract.ps1') -RepositoryRoot $projectRoot
& (Join-Path $projectRoot 'tests\ReleaseScripts.SelfTest.ps1')

dotnet restore $solution --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

dotnet build $solution -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

dotnet run --project $testProject -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Core self-tests failed.' }

if (-not (Test-Path -LiteralPath $pluginTestAssembly -PathType Leaf)) {
    throw "Plugin persistence self-test assembly not found: $pluginTestAssembly"
}
dotnet $pluginTestAssembly
if ($LASTEXITCODE -ne 0) { throw 'Plugin persistence self-tests failed.' }

if (-not (Test-Path -LiteralPath $sourceZip -PathType Leaf)) {
    throw "Dalamud packager output not found: $sourceZip"
}

$artifact = Save-SeitonVersionedArchive -BuiltArchive $sourceZip `
    -OutputDirectory $resolvedOutput -Version $version
$releaseZip = $artifact.ArchivePath
$latestZip = Join-Path $resolvedOutput 'latest.zip'
$hash = $artifact.Hash
$sourceFingerprint = (& (Join-Path $PSScriptRoot 'Get-SourceFingerprint.ps1') -RepositoryRoot $projectRoot).Trim()
Set-Content -LiteralPath (Join-Path $resolvedOutput 'source.sha256') -Value $sourceFingerprint -Encoding ascii

& (Join-Path $PSScriptRoot 'Verify-Release.ps1') `
    -ArchivePath $releaseZip `
    -RepositoryRoot $projectRoot `
    -SourceFingerprintPath (Join-Path $resolvedOutput 'source.sha256')
if ($LASTEXITCODE -ne 0) { throw 'Release verification failed.' }

$latestHash = (Get-FileHash -LiteralPath $latestZip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($latestHash -ne $hash) { throw 'dist/latest.zip differs from the versioned release.' }

Write-Host "Release: $releaseZip"
Write-Host "SHA-256: $hash"
if ($artifact.Reused) { Write-Host 'Reused the unchanged versioned archive and checksum.' }
