param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$sourceRoot = Join-Path $resolvedRoot 'src'
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
if ($sourceFiles.Count -eq 0) { throw 'No C# source files found.' }

$forbiddenChecks = [ordered]@{
    'network client APIs' = '\b(HttpClient|WebRequest|TcpClient|UdpClient|Socket|WebSocket)\b'
    'hooks or signature scans' = '\b(IGameInteropProvider|Hook<|HookFromAddress|SignatureAttribute|SigScanner)\b'
    'game action execution' = '\b(UseAction|UseActionLocation|ExecuteAction|SendAction)\b'
    'target mutation services' = '\b(ITargetManager|TargetManager)\b'
    'input injection' = '\b(SendInput|keybd_event|mouse_event|ExecuteCommand)\b'
    'gameplay file writes' = '\b(File\.Write|FileStream|StreamWriter|Directory\.CreateDirectory)\b'
}

foreach ($check in $forbiddenChecks.GetEnumerator()) {
    $matches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern $check.Value)
    if ($matches.Count -gt 0) {
        $locations = $matches | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
        throw "Display-only safety contract failed ($($check.Key)): $($locations -join ', ')"
    }
}

$unsafeMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bunsafe\b')
$allowedUnsafe = @(
    (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\EnemySlotResolver.cs'),
    (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\SeitonReadinessProbe.cs')
)
$unexpectedUnsafe = @($unsafeMatches | Where-Object { $allowedUnsafe -notcontains $_.Path })
if ($unexpectedUnsafe.Count -gt 0) {
    throw "Unsafe code is allowed only in the two read-only probes: $($unexpectedUnsafe.Path -join ', ')"
}
foreach ($allowed in $allowedUnsafe) {
    if (-not ($unsafeMatches.Path -contains $allowed)) { throw "Expected read-only unsafe probe is missing: $allowed" }
}

$slotResolver = Get-Content -LiteralPath $allowedUnsafe[0] -Raw
if ($slotResolver -notmatch 'ResolvePlaceholder\(\$"<e\{slot\}>"\s*,\s*1\s*,\s*0\s*\)') {
    throw 'Enemy slots must come from exact native <e1>-<e5> placeholder resolution.'
}
if ($slotResolver -match '\b(Write|Set|UseAction|TargetManager)\b') {
    throw 'Enemy slot resolver must remain read-only.'
}

$readiness = Get-Content -LiteralPath $allowedUnsafe[1] -Raw
foreach ($required in @(
    'GetAdjustedActionId',
    'GetActionStatus',
    'GetActionInRangeOrLoS',
    'CanUseActionOnTarget',
    'IsActionOffCooldown',
    'LimitBreakController.Instance',
    'BaseActionId = 29515',
    'FollowUpActionId = 29516',
    'UnsealedStatusId = 3192',
    'MaximumRange = 20f')) {
    if ($readiness -notmatch [regex]::Escape($required)) { throw "Readiness probe is missing required gate: $required" }
}
if ($readiness -match '\b(UseAction|UseActionLocation|ActionQueued|QueuedAction)\b') {
    throw 'Readiness probe must never execute or queue an action.'
}

$tracker = Get-Content -LiteralPath (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\ExecuteTracker.cs') -Raw
foreach ($required in @('ExecuteThreshold.IsBelowHalf', 'IsAvailableForTarget', 'EnemySlotResolver.Resolve')) {
    if ($tracker -notmatch [regex]::Escape($required)) { throw "Tracker is missing required fail-closed gate: $required" }
}

$metadata = Get-Content -LiteralPath (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\SeitonMetadataGuard.cs') -Raw
foreach ($required in @(
    'SeitonReadinessProbe.BaseActionId',
    'SeitonReadinessProbe.FollowUpActionId',
    'SeitonReadinessProbe.UnsealedStatusId',
    'Seiton Tenchu',
    'Unsealed Seiton Tenchu',
    'RequiresLineOfSight')) {
    if ($metadata -notmatch [regex]::Escape($required)) { throw "Metadata guard is missing required proof: $required" }
}

Write-Host "Display-only safety contract verified across $($sourceFiles.Count) source files; only exact slot and Seiton readiness reads are allowed."
