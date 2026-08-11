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
    'native UI mutation' = '\b(LoadIconTexture|UnloadTexture|ToggleVisibility|SetPosition|SetScale|Destroy)\s*\('
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
    (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\SeitonReadinessProbe.cs'),
    (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\NamePlateAnchorTracker.cs')
)
$unexpectedUnsafe = @($unsafeMatches | Where-Object { $allowedUnsafe -notcontains $_.Path })
if ($unexpectedUnsafe.Count -gt 0) {
    throw "Unsafe code is allowed only in the three read-only probes: $($unexpectedUnsafe.Path -join ', ')"
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
    'GetActionInRangeOrLoS',
    'IsActionOffCooldown',
    'LimitBreakController.Instance',
    'TryGetReadyAction',
    'HasRangeAndLineOfSight',
    'BaseActionId = 29515',
    'FollowUpActionId = 29516',
    'UnsealedStatusId = 3192',
    'MaximumRange = 20f')) {
    if ($readiness -notmatch [regex]::Escape($required)) { throw "Readiness probe is missing required gate: $required" }
}
if ($readiness -match '\b(UseAction|UseActionLocation|ActionQueued|QueuedAction)\b') {
    throw 'Readiness probe must never execute or queue an action.'
}
if ($readiness -match '\b(GetActionStatus|CanUseActionOnTarget)\b') {
    throw 'Readiness must not use transient target/facing/action-lock gates that caused the old flicker.'
}

$namePlateAnchor = Get-Content -LiteralPath $allowedUnsafe[2] -Raw
foreach ($required in @(
    'INamePlateGui',
    'OnDataUpdate',
    'OnPostDataUpdate',
    'GetAddonByName<AddonNamePlate>',
    'NamePlateIndex',
    'NamePlateObjectArray',
    'NamePlateObjectAddress != (nint)plate',
    'NameIcon',
    'GetBounds')) {
    if ($namePlateAnchor -notmatch [regex]::Escape($required)) { throw "Nameplate anchor is missing read-only proof: $required" }
}
if ($namePlateAnchor -match '\b(LoadIconTexture|UnloadTexture|ToggleVisibility|SetPosition|SetScale|Destroy)\s*\(') {
    throw 'Nameplate integration must copy bounds only and never mutate native UI nodes.'
}

$tracker = Get-Content -LiteralPath (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\ExecuteTracker.cs') -Raw
foreach ($required in @(
    'ExecuteThreshold.IsBelowHalf',
    'TryGetReadyAction',
    'HasRangeAndLineOfSight',
    'EnemySlotResolver.Resolve',
    'DebouncedVisibilityRules.Observe',
    'GuardCooldownRules.ObserveStatus',
    'LowMpRules.Observe',
    'StablePopupRules.Observe')) {
    if ($tracker -notmatch [regex]::Escape($required)) { throw "Tracker is missing required fail-closed gate: $required" }
}

$rangeRules = Get-Content -LiteralPath (Join-Path $sourceRoot 'SeitonSense.Core\SeitonRangeRules.cs') -Raw
foreach ($required in @('Ready = 0', 'NotFacingTarget = 565', 'HasNativeRangeAndLineOfSight')) {
    if ($rangeRules -notmatch [regex]::Escape($required)) { throw "Range allowlist is missing required proof: $required" }
}
if ($readiness -notmatch [regex]::Escape('SeitonRangeRules.HasNativeRangeAndLineOfSight')) {
    throw 'Readiness probe must use the strict native range and line-of-sight allowlist.'
}

$metadata = Get-Content -LiteralPath (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\SeitonMetadataGuard.cs') -Raw
foreach ($required in @(
    'SeitonReadinessProbe.BaseActionId',
    'SeitonReadinessProbe.FollowUpActionId',
    'SeitonReadinessProbe.UnsealedStatusId',
    'Seiton Tenchu',
    'Unsealed Seiton Tenchu',
    'RequiresLineOfSight',
    'EnemyCombatConstants.GuardActionId',
    'EnemyCombatConstants.RecuperateActionId',
    'EnemyCombatConstants.RecuperateMpCost')) {
    if ($metadata -notmatch [regex]::Escape($required)) { throw "Metadata guard is missing required proof: $required" }
}

$combatConstants = Get-Content -LiteralPath (Join-Path $sourceRoot 'SeitonSense.Plugin\Services\EnemyCombatConstants.cs') -Raw
$guardRules = Get-Content -LiteralPath (Join-Path $sourceRoot 'SeitonSense.Core\GuardCooldownRules.cs') -Raw
$mpRules = Get-Content -LiteralPath (Join-Path $sourceRoot 'SeitonSense.Core\LowMpRules.cs') -Raw
foreach ($pair in @(
    @($combatConstants, 'GuardDurationSeconds = 4f'),
    @($combatConstants, 'GuardCooldownSeconds = 30f'),
    @($combatConstants, 'RecuperateMpCost = 2000'),
    @($combatConstants, 'LowMpExitThreshold = 2300'),
    @($guardRules, 'CooldownMilliseconds = 30_000'),
    @($guardRules, 'ActiveDurationMilliseconds = 4_000'),
    @($mpRules, 'RecuperateCost = 2_000'),
    @($mpRules, 'ExitThreshold = 2_300'))) {
    if ($pair[0] -notmatch [regex]::Escape($pair[1])) {
        throw "Core/runtime combat constants drifted: $($pair[1])"
    }
}

Write-Host "Display-only safety contract verified across $($sourceFiles.Count) source files; exact slots, status/resource reads, and read-only nameplate bounds are allowed."
