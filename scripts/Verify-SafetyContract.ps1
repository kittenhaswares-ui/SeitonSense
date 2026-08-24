param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$sourceRoot = Join-Path $resolvedRoot 'src'
$pluginServicesRoot = Join-Path $sourceRoot 'SeitonSense.Plugin\Services'
$pluginUiRoot = Join-Path $sourceRoot 'SeitonSense.Plugin\UI'
$overlayRendererPath = Join-Path $pluginUiRoot 'OverlayRenderer.cs'
$coreRoot = Join-Path $sourceRoot 'SeitonSense.Core'
$coreSelfTestRoot = Join-Path $resolvedRoot 'tests\SeitonSense.Core.SelfTest'
$autoLowMpFocusTargetServicePath = Join-Path $pluginServicesRoot 'AutoLowMpFocusTargetService.cs'
$autoLowMpFocusTargetRulesPath = Join-Path $coreRoot 'AutoLowMpFocusTargetRules.cs'
$autoLowMpFocusTargetSelfTestsPath = Join-Path $coreSelfTestRoot 'AutoLowMpFocusTargetSelfTests.cs'
$darkKnightShadowbringerServicePath = Join-Path $pluginServicesRoot 'DarkKnightShadowbringerMacroService.cs'
$darkKnightShadowbringerRulesPath = Join-Path $coreRoot 'DarkKnightShadowbringerMacroRules.cs'
$darkKnightShadowbringerSelfTestsPath = Join-Path $coreSelfTestRoot 'DarkKnightShadowbringerMacroSelfTests.cs'
$panicShukuchiServicePath = Join-Path $pluginServicesRoot 'PanicShukuchiService.cs'
$panicShukuchiRulesPath = Join-Path $coreRoot 'PanicShukuchiRules.cs'
$panicShukuchiSelfTestsPath = Join-Path $coreSelfTestRoot 'PanicShukuchiSelfTests.cs'
$ninjaGuardShukuchiProbePath = Join-Path $pluginServicesRoot 'NinjaGuardShukuchiProbe.cs'
$ninjaGuardShukuchiRulesPath = Join-Path $coreRoot 'NinjaGuardShukuchiRules.cs'
$ninjaGuardShukuchiSelfTestsPath = Join-Path $coreSelfTestRoot 'NinjaGuardShukuchiSelfTests.cs'
$darkKnightPlungeProbePath = Join-Path $pluginServicesRoot 'DarkKnightPlungeProbe.cs'
$darkKnightPlungeRulesPath = Join-Path $coreRoot 'DarkKnightPlungeRules.cs'
$darkKnightPlungeSelfTestsPath = Join-Path $coreSelfTestRoot 'DarkKnightPlungeSelfTests.cs'
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
if ($sourceFiles.Count -eq 0) { throw 'No C# source files found.' }

function Read-RequiredSource([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label source is missing: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8
}

function Assert-Literals([string]$Content, [string[]]$Required, [string]$Label) {
    foreach ($literal in $Required) {
        if ($Content -notmatch [regex]::Escape($literal)) {
            throw "$Label is missing required proof: $literal"
        }
    }
}

$forbiddenChecks = [ordered]@{
    'network client APIs' = '\b(HttpClient|HttpClientFactory|HttpRequestMessage|WebRequest|TcpClient|UdpClient|Socket|ClientWebSocket|WebSocket)\b|\bSystem\.Net(?:\.|\b)'
    'signature scans or unmanaged hook libraries' = '\b(SignatureAttribute|SigScanner|MinHook)\b'
    'target mutation services' = '(?-i:\bTargetManager\b)|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)'
    'native UI or input injection' = '\b(SendInput|keybd_event|mouse_event|ExecuteCommand|SetRawValue|ClearAll|FireCallback|SendEvent)\b'
    'gameplay file writes' = '\b(File\.Write|FileStream|StreamWriter|Directory\.CreateDirectory)\b'
    'native UI mutation' = '\b(LoadIconTexture|UnloadTexture|ToggleVisibility|SetPosition|SetScale|SetAlpha|SetAdditive|SetMultiply|SetColor|Destroy|PulseActionBarSlot)\s*\('
}

foreach ($check in $forbiddenChecks.GetEnumerator()) {
    $matches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern $check.Value)
    if ($check.Key -eq 'target mutation services') {
        # Auto Low-MP Focus owns one reviewed empty-to-exact FocusTarget write.
        # NIN Guard-Shukuchi owns one exact accepted-action hard-target write.
        # Every other managed or native target mutation remains globally fatal.
        $matches = @($matches | Where-Object {
            -not (($_.Path -eq $autoLowMpFocusTargetServicePath -and
                   $_.Line -match '^\s*targetManager\.FocusTarget\s*=\s*exactTarget;\s*$') -or
                  ($_.Path -eq $ninjaGuardShukuchiProbePath -and
                   $_.Line -match '^\s*targetManager\.Target\s*=\s*target;\s*$'))
        })
    }
    if ($matches.Count -gt 0) {
        $locations = $matches | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
        throw "Safety contract failed ($($check.Key)): $($locations -join ', ')"
    }
}

$slotResolverPath = Join-Path $pluginServicesRoot 'EnemySlotResolver.cs'
$readinessPath = Join-Path $pluginServicesRoot 'SeitonReadinessProbe.cs'
$namePlateAnchorPath = Join-Path $pluginServicesRoot 'NamePlateAnchorTracker.cs'
$inputContextPath = Join-Path $pluginServicesRoot 'GameInputContextProbe.cs'
$purifyProbePath = Join-Path $pluginServicesRoot 'EmergencyPurifyProbe.cs'
$emergencyInputCoordinatorPath = Join-Path $pluginServicesRoot 'EmergencyActionInputCoordinator.cs'
$clientActionAttemptBoundaryPath = Join-Path $pluginServicesRoot 'ClientActionAttemptBoundary.cs'
$clientActionAttemptOutcomePath = Join-Path $coreRoot 'ClientActionAttemptOutcome.cs'
$heldActionRetryRulesPath = Join-Path $coreRoot 'HeldActionRetryRules.cs'
$heldActionRetrySelfTestsPath = Join-Path $coreSelfTestRoot 'HeldActionRetrySelfTests.cs'
$physicalGameplayKeyRulesPath = Join-Path $coreRoot 'PhysicalGameplayKeyRules.cs'
$physicalGameplayKeySelfTestsPath = Join-Path $coreSelfTestRoot 'PhysicalGameplayKeySelfTests.cs'
$heldCastCancellationRulesPath = Join-Path $coreRoot 'HeldCastCancellationRules.cs'
$heldCastCancellationServicePath = Join-Path $pluginServicesRoot 'HeldCastCancellationService.cs'
$heldCastCancellationSelfTestsPath = Join-Path $coreSelfTestRoot 'HeldCastCancellationSelfTests.cs'
$emergencyPurifyBufferRulesPath = Join-Path $coreRoot 'EmergencyPurifyBufferRules.cs'
$allyRescueBufferRulesPath = Join-Path $coreRoot 'AllyRescueBufferRules.cs'
$miracleInterceptRulesPath = Join-Path $coreRoot 'MiracleInterceptRules.cs'
$allyRescueProbePath = Join-Path $pluginServicesRoot 'AllyRescueProbe.cs'
$miracleInterceptProbePath = Join-Path $pluginServicesRoot 'MiracleInterceptProbe.cs'
$defensiveUtilityProbePath = Join-Path $pluginServicesRoot 'DefensiveUtilityProbe.cs'
$pressureEscapeSprintProbePath = Join-Path $pluginServicesRoot 'PressureEscapeSprintProbe.cs'
$highPressureWarningSoundPath = Join-Path $pluginServicesRoot 'HighPressureWarningSound.cs'
$ninjaSeitonProbePath = Join-Path $pluginServicesRoot 'NinjaSeitonDispatchProbe.cs'
$ninjaSeitonProtectionProbePath = Join-Path $pluginServicesRoot 'NinjaSeitonProtectionProbe.cs'
$scholarCriticalStrategyProbePath = Join-Path $pluginServicesRoot 'ScholarCriticalStrategyProbe.cs'
$isolationAwarenessPath = Join-Path $pluginServicesRoot 'IsolationAwarenessService.cs'
$autoEnemyFocusMarkPath = Join-Path $pluginServicesRoot 'AutoEnemyFocusMarkService.cs'
$reviewedPvpCommandDispatcherPath = Join-Path $pluginServicesRoot 'ReviewedPvpCommandDispatcher.cs'
$guardianCommunicationPath = Join-Path $pluginServicesRoot 'GuardianCommunicationService.cs'
$guardianCommunicationMetadataGuardPath = Join-Path $pluginServicesRoot 'GuardianCommunicationMetadataGuard.cs'
$monkEarthReplyProbePath = Join-Path $pluginServicesRoot 'MonkEarthReplyProbe.cs'
$resourceAuraAnchorPath = Join-Path $pluginServicesRoot 'ResourceAuraAnchorTracker.cs'
$allyRescueConfirmationRulesPath = Join-Path $coreRoot 'AllyRescueConfirmationRules.cs'
$miracleCleanseFollowupRulesPath = Join-Path $coreRoot 'MiracleCleanseFollowupRules.cs'
$miracleCleanseFollowupSelfTestsPath = Join-Path $coreSelfTestRoot 'MiracleCleanseFollowupSelfTests.cs'
$miracleGuardFollowupRulesPath = Join-Path $coreRoot 'MiracleGuardFollowupRules.cs'
$miracleGuardFollowupSelfTestsPath = Join-Path $coreSelfTestRoot 'MiracleGuardFollowupSelfTests.cs'
$miracleProtectionEndRulesPath = Join-Path $coreRoot 'MiracleProtectionEndRules.cs'
$miracleProtectionEndSelfTestsPath = Join-Path $coreSelfTestRoot 'MiracleProtectionEndSelfTests.cs'
$defensiveUtilityRulesPath = Join-Path $coreRoot 'DefensiveUtilityRules.cs'
$defensiveUtilitySelfTestsPath = Join-Path $coreSelfTestRoot 'DefensiveUtilitySelfTests.cs'
$autoGuardProtectionRulesPath = Join-Path $coreRoot 'AutoGuardProtectionRules.cs'
$pressureEscapeRulesPath = Join-Path $coreRoot 'PressureEscapeRules.cs'
$pressureEscapeSelfTestsPath = Join-Path $coreSelfTestRoot 'PressureEscapeSelfTests.cs'
$ninjaSeitonDispatchRulesPath = Join-Path $coreRoot 'NinjaSeitonDispatchRules.cs'
$ninjaSeitonDispatchSelfTestsPath = Join-Path $coreSelfTestRoot 'NinjaSeitonDispatchSelfTests.cs'
$isolationWarningRulesPath = Join-Path $coreRoot 'IsolationWarningRules.cs'
$autoEnemyFocusMarkRulesPath = Join-Path $coreRoot 'AutoEnemyFocusMarkRules.cs'
$guardianTeamCommunicationRulesPath = Join-Path $coreRoot 'GuardianTeamCommunicationRules.cs'
$guardianTeamCommunicationSelfTestsPath = Join-Path $coreSelfTestRoot 'GuardianTeamCommunicationSelfTests.cs'
$scholarCriticalStrategyRulesPath = Join-Path $coreRoot 'ScholarCriticalStrategyRules.cs'
$scholarCriticalStrategySelfTestsPath = Join-Path $coreSelfTestRoot 'ScholarCriticalStrategySelfTests.cs'
$smartKardiaRulesPath = Join-Path $coreRoot 'SmartKardiaRules.cs'
$smartKardiaProbePath = Join-Path $pluginServicesRoot 'SmartKardiaProbe.cs'
$smartKardiaSelfTestsPath = Join-Path $coreSelfTestRoot 'SmartKardiaSelfTests.cs'
$smartRecuperateRulesPath = Join-Path $coreRoot 'SmartRecuperateRules.cs'
$smartRecuperateProbePath = Join-Path $pluginServicesRoot 'SmartRecuperateProbe.cs'
$smartRecuperateSelfTestsPath = Join-Path $coreSelfTestRoot 'SmartRecuperateSelfTests.cs'
$combatLimitBreakCatalogPath = Join-Path $coreRoot 'CombatLimitBreakCatalog.cs'
$combatLimitBreakEventRulesPath = Join-Path $coreRoot 'CombatLimitBreakEventRules.cs'
$combatLimitBreakSelfTestsPath = Join-Path $coreSelfTestRoot 'CombatLimitBreakSelfTests.cs'
$combatLimitBreakCaptureBufferPath = Join-Path $pluginServicesRoot 'CombatLimitBreakCaptureBuffer.cs'
$combatLimitBreakMetadataGuardPath = Join-Path $pluginServicesRoot 'CombatLimitBreakMetadataGuard.cs'
$combatLimitBreakRuntimeServicePath = Join-Path $pluginServicesRoot 'CombatLimitBreakRuntimeService.cs'
$combatLimitBreakNameplateRulesPath = Join-Path $coreRoot 'CombatLimitBreakNameplateRules.cs'
$combatLimitBreakNameplateSelfTestsPath = Join-Path $coreSelfTestRoot 'CombatLimitBreakNameplateSelfTests.cs'
$combatLimitBreakNotificationRulesPath = Join-Path $coreRoot 'CombatLimitBreakNotificationRules.cs'
$combatLimitBreakNotificationSelfTestsPath = Join-Path $coreSelfTestRoot 'CombatLimitBreakNotificationSelfTests.cs'
$localMpWarningRulesPath = Join-Path $coreRoot 'LocalMpWarningRules.cs'
$localMpWarningSelfTestsPath = Join-Path $coreSelfTestRoot 'LocalMpWarningSelfTests.cs'
$smartTargetSelectionRulesPath = Join-Path $coreRoot 'SmartTargetSelectionRules.cs'
$smartTargetSelectionSelfTestsPath = Join-Path $coreSelfTestRoot 'SmartTargetSelectionSelfTests.cs'
$limitBreakNotificationRendererPath = Join-Path $pluginUiRoot 'LimitBreakNotificationRenderer.cs'
$overlayRendererLimitBreaksPath = Join-Path $pluginUiRoot 'OverlayRenderer.LimitBreaks.cs'
$autoSeitonToggleWindowPath = Join-Path $pluginUiRoot 'AutoSeitonToggleWindow.cs'
$whatsNewWindowPath = Join-Path $pluginUiRoot 'WhatsNewWindow.cs'
$smartWardensPaeanRulesPath = Join-Path $coreRoot 'SmartWardensPaeanTargetRules.cs'
$smartWardensPaeanServicePath = Join-Path $pluginServicesRoot 'SmartWardensPaeanService.cs'
$smartWardensPaeanSelfTestsPath = Join-Path $coreSelfTestRoot 'SmartWardensPaeanTargetSelfTests.cs'
$nearAssistPath = Join-Path $pluginServicesRoot 'NearAssistRedirector.cs'
$partySlotResolverPath = Join-Path $pluginServicesRoot 'PartySlotResolver.cs'
$machinistLimitBreakCapturePath = Join-Path $pluginServicesRoot 'MachinistLimitBreakCapture.cs'
$machinistLimitBreakWarningSoundPath = Join-Path $pluginServicesRoot 'MachinistLimitBreakWarningSound.cs'
$targetPressureTrackerPath = Join-Path $pluginServicesRoot 'TargetPressureTracker.cs'
$targetPressureSnapshotPath = Join-Path $pluginServicesRoot 'TargetPressureSnapshot.cs'
$ccProtectionMetadataGuardPath = Join-Path $pluginServicesRoot 'CcProtectionMetadataGuard.cs'
$ccImmunityBrakeServicePath = Join-Path $pluginServicesRoot 'CcImmunityBrakeService.cs'
$ccImmunityBrakeMetadataGuardPath = Join-Path $pluginServicesRoot 'CcImmunityBrakeMetadataGuard.cs'
$ccImmunityBrakeTargetRulesPath = Join-Path $coreRoot 'CcImmunityBrakeTargetRules.cs'
$personalStatusPath = Join-Path $pluginServicesRoot 'PersonalStatusService.cs'
$wolvesDenResolverPath = Join-Path $pluginServicesRoot 'WolvesDenOpponentResolver.cs'
$pluginPath = Join-Path $sourceRoot 'SeitonSense.Plugin\Plugin.cs'
$targetHighlightPath = Join-Path $pluginUiRoot 'TargetHighlightRenderer.cs'
$pressureCounterPath = Join-Path $pluginUiRoot 'PressureCounterWindow.cs'
$settingsPartsRoot = Join-Path $pluginUiRoot 'Settings'
$settingsSourceFiles = @()
$settingsSourceFiles += @(Get-ChildItem -LiteralPath $pluginUiRoot -Filter 'SettingsWindow*.cs' -File)
$settingsSourceFiles += @(Get-ChildItem -LiteralPath $settingsPartsRoot -Filter '*.cs' -File -Recurse)
$settingsSourceFiles = @($settingsSourceFiles | Sort-Object -Property FullName -Unique)
if ($settingsSourceFiles.Count -eq 0) {
    throw 'No SettingsWindow source files found.'
}

$expectedSettingsRelativePaths = @(
    'SettingsWindow.cs',
    'Settings/SettingsWindow.Actions.cs',
    'Settings/SettingsWindow.Alerts.cs',
    'Settings/SettingsWindow.Diagnostics.cs',
    'Settings/SettingsWindow.Hud.cs',
    'Settings/SettingsWindow.Jobs.cs',
    'Settings/SettingsWindow.Macros.cs',
    'Settings/SettingsWindow.Start.cs',
    'Settings/SettingsWindow.Targets.cs',
    'Settings/SettingsWindow.Widgets.cs'
)
$settingsRelativePaths = @($settingsSourceFiles | ForEach-Object {
    [System.IO.Path]::GetRelativePath($pluginUiRoot, $_.FullName).Replace('\', '/')
})
$settingsLayoutDifference = @(
    Compare-Object -ReferenceObject $expectedSettingsRelativePaths -DifferenceObject $settingsRelativePaths
)
if ($settingsLayoutDifference.Count -ne 0) {
    $layoutDetails = $settingsLayoutDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
    throw "SettingsWindow split layout drifted: $($layoutDetails -join ', ')"
}

$settingsWindow = ($settingsSourceFiles | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
}) -join "`n"
$normalizedSettingsWindow = $settingsWindow -replace '\s+', ' '
$allowedUnsafe = @(
    $slotResolverPath,
    $readinessPath,
    $namePlateAnchorPath,
    $inputContextPath,
    $purifyProbePath,
    $clientActionAttemptBoundaryPath,
    $allyRescueProbePath,
    $miracleInterceptProbePath,
    $defensiveUtilityProbePath,
    $pressureEscapeSprintProbePath,
    $ninjaSeitonProbePath,
    $scholarCriticalStrategyProbePath,
    $smartKardiaProbePath,
    $smartRecuperateProbePath,
    $smartWardensPaeanServicePath,
    $heldCastCancellationServicePath,
    $isolationAwarenessPath,
    $autoEnemyFocusMarkPath,
    $reviewedPvpCommandDispatcherPath,
    $guardianCommunicationPath,
    $monkEarthReplyProbePath,
    $resourceAuraAnchorPath,
    $nearAssistPath,
    $partySlotResolverPath,
    $machinistLimitBreakCapturePath,
    $machinistLimitBreakWarningSoundPath,
    $targetPressureTrackerPath,
    $ccImmunityBrakeServicePath,
    $autoLowMpFocusTargetServicePath,
    $darkKnightShadowbringerServicePath,
    $panicShukuchiServicePath,
    $ninjaGuardShukuchiProbePath,
    $darkKnightPlungeProbePath,
    $combatLimitBreakCaptureBufferPath
)

$unsafeMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bunsafe\b')
$unexpectedUnsafe = @($unsafeMatches | Where-Object { $allowedUnsafe -notcontains $_.Path })
if ($unexpectedUnsafe.Count -gt 0) {
    $locations = $unexpectedUnsafe | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Unsafe code is allowed only in the reviewed native boundaries: $($locations -join ', ')"
}

# Near Assist, Near Help, and Far Help share one target-only action detour. The
# MCH/pressure capture owns the sole read-only ActionEffect receive hook and
# forwards value-only activation/damage records to the bounded Combat LB buffer.
# Plugin.cs only constructor-injects interop.
$interopMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(IGameInteropProvider|Hook<|HookFromAddress)\b')
$unexpectedInterop = @($interopMatches | Where-Object {
    $_.Path -notin @($pluginPath, $nearAssistPath, $machinistLimitBreakCapturePath)
})
if ($unexpectedInterop.Count -gt 0) {
    $locations = $unexpectedInterop | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only Near Assist and the read-only MCH/pressure capture may own native hooks: $($locations -join ', ')"
}
$pluginSource = Read-RequiredSource $pluginPath 'Plugin entry point'
if ([regex]::Matches($pluginSource, '\bIGameInteropProvider\b').Count -ne 1 -or
    $pluginSource -match '\b(Hook<|HookFromAddress)\b') {
    throw 'Plugin.cs may only constructor-inject one IGameInteropProvider; it may not create a hook.'
}
Assert-Literals $pluginSource @(
    'NearAssistCommand = "/nearassist"',
    'NearAssistAliasCommand = "/ssassist"',
    'NearHelpCommand = "/nearhelp"',
    'NearHelpAliasCommand = "/sshelp"',
    'CC-only survival-target helper: bounded pressure, plus exact self when the action allows it.',
    '/nearhelp and /sshelp arm the one-shot survival-target helper (pressure/self when the action allows).',
    'FarHelpCommand = "/farhelp"',
    'FarHelpAliasCommand = "/ssfar"',
    'new NearAssistRedirector(',
    'AllowedInMacros = true',
    'nearAssistCommandRegistered = commandManager.AddHandler(',
    'if (nearAssistCommandRegistered) commandManager.RemoveHandler(NearAssistCommand)',
    'if (nearAssistAliasRegistered) commandManager.RemoveHandler(NearAssistAliasCommand)',
    'if (nearHelpCommandRegistered) commandManager.RemoveHandler(NearHelpCommand)',
    'if (nearHelpAliasRegistered) commandManager.RemoveHandler(NearHelpAliasCommand)',
    'farHelpCommandRegistered = commandManager.AddHandler(',
    'farHelpAliasRegistered = commandManager.AddHandler(',
    'new CommandInfo(OnFarHelpCommand)',
    'nearAssist.ArmFarHelp()',
    'if (farHelpCommandRegistered) commandManager.RemoveHandler(FarHelpCommand)',
    'if (farHelpAliasRegistered) commandManager.RemoveHandler(FarHelpAliasCommand)',
    'nearAssist.Dispose()'
) 'Near Assist, Near Help, and Far Help command ownership and lifecycle'
if ($pluginSource -match 'lowest-health ally helper') {
    throw 'Near Help command copy must describe survival targeting with bounded pressure and action-gated self eligibility.'
}
foreach ($allowed in $allowedUnsafe) {
    if (-not (Test-Path -LiteralPath $allowed -PathType Leaf)) {
        throw "Expected narrow probe is missing: $allowed"
    }
    if (-not ($unsafeMatches.Path -contains $allowed)) {
        throw "Expected narrow probe contains no explicit unsafe boundary: $allowed"
    }
}

# Target highlighting may read the current and focus targets in one dedicated
# renderer. Auto Low-MP Focus owns one reviewed empty-to-exact FocusTarget write.
# NIN Guard-Shukuchi owns one accepted-only exact actor target
# write. Plugin.cs and PersonalStatusService.cs only inject the API.
$targetManagerMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bITargetManager\b')
$unexpectedTargetManager = @($targetManagerMatches | Where-Object {
    $_.Path -notin @(
        $pluginPath,
        $personalStatusPath,
        $targetHighlightPath,
        $autoLowMpFocusTargetServicePath,
        $ninjaGuardShukuchiProbePath)
})
if ($unexpectedTargetManager.Count -gt 0) {
    $locations = $unexpectedTargetManager | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "ITargetManager is allowed only for constructor injection, the read-only renderer, Auto Low-MP Focus, and accepted-only NIN Guard-Shukuchi targeting: $($locations -join ', ')"
}
$targetHighlight = Read-RequiredSource $targetHighlightPath 'Target highlight renderer'
Assert-Literals $targetHighlight @(
    'targetManager.Target',
    'targetManager.FocusTarget',
    'TargetHighlightRules.BuildPlan',
    'DrawCurrentTargetInfoHud',
    '!tracker.IsActive',
    'fixed HUD card',
    'never attaches anything to a nameplate'
) 'Read-only target highlight renderer'
if ($targetHighlight -match '\b(SetTarget|UseAction|UseActionLocation)\b' -or
    $targetHighlight -match '(?-i:\bTargetManager\b)' -or
    $targetHighlight -match '\.(Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)' -or
    $targetHighlight -match '\b(INamePlateGui|NamePlateAnchorTracker|NamePlateObject|NameIcon)\b') {
    throw 'Target highlighting must remain read-only and separate from native nameplates and existing icon slots.'
}
if ($targetHighlight -match '(?m)^\s*private\s+(?:readonly\s+)?IGameObject\??\s+') {
    throw 'Target wrappers must be resolved and discarded within the current draw frame.'
}

# There are exactly two reviewed managed target-property setter sites: one
# frozen FocusTarget write and one accepted/revalidated Shukuchi hard-target write.
$managedTargetSetterMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\.(Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)')
$reviewedFocusSetterMatches = @($managedTargetSetterMatches | Where-Object {
    $_.Path -eq $autoLowMpFocusTargetServicePath -and
    $_.Line -match '^\s*targetManager\.FocusTarget\s*=\s*exactTarget;\s*$'
})
$reviewedNinjaGuardShukuchiHardSetterMatches = @($managedTargetSetterMatches | Where-Object {
    $_.Path -eq $ninjaGuardShukuchiProbePath -and
    $_.Line -match '^\s*targetManager\.Target\s*=\s*target;\s*$'
})
if ($managedTargetSetterMatches.Count -ne 2 -or
    $reviewedFocusSetterMatches.Count -ne 1 -or
    $reviewedNinjaGuardShukuchiHardSetterMatches.Count -ne 1) {
    $locations = $managedTargetSetterMatches | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }
    throw "Exactly two reviewed managed target setter sites are allowed: $($locations -join ', ')"
}
$nativeTargetSetterMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '(?-i:\b(?:TargetManager|TargetSystem|SetTarget|SetFocusTarget|SetSoftTarget|SetMouseOverTarget|SetMouseOverNameplateTarget|SetGPoseTarget)\b)|->\s*(?:Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)')
if ($nativeTargetSetterMatches.Count -gt 0) {
    $locations = $nativeTargetSetterMatches | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Native target setters remain forbidden: $($locations -join ', ')"
}

# Auto Low-MP Focus is a default-off, exact-Crystalline-Conflict-only
# empty-to-exact FocusTarget mutation. Core owns the trusted inclusive <=2,000
# MP wave, empty-focus stability, one-attempt/rate policy, frozen intent, and
# permanent manual-drift latch until an explicit internal reset.
$autoLowMpFocusTargetRules = Read-RequiredSource $autoLowMpFocusTargetRulesPath 'Auto Low-MP Focus rules'
$normalizedAutoLowMpFocusTargetRules = $autoLowMpFocusTargetRules -replace '\s+', ' '
$autoLowMpFocusTargetService = Read-RequiredSource $autoLowMpFocusTargetServicePath 'Auto Low-MP Focus runtime'
$normalizedAutoLowMpFocusTargetService = $autoLowMpFocusTargetService -replace '\s+', ' '
$autoLowMpFocusTargetSelfTests = Read-RequiredSource $autoLowMpFocusTargetSelfTestsPath 'Auto Low-MP Focus self-tests'
$autoLowMpFocusTargetProgram = Read-RequiredSource (
    Join-Path $coreSelfTestRoot 'Program.cs') 'Core self-test registration'

Assert-Literals $autoLowMpFocusTargetRules @(
    'public enum AutoLowMpFocusObservedState',
    'Unknown = 0',
    'Empty = 1',
    'Occupied = 2',
    'public enum AutoLowMpFocusTargetSetOutcome',
    'TerminalFailure = 0',
    'SetterInvokedWithoutExactReadback = 1',
    'ExactReadbackConfirmed = 2',
    'bool LowMpWaveActive',
    'bool AttemptSpentForWave',
    'bool ManualOverrideLatched',
    'long FocusEmptySinceMilliseconds',
    'long LastAttemptAtMilliseconds',
    'TargetPressureActorIdentity LastConfirmedFocusTarget',
    'public const uint ProbeActionId = 29_515',
    'public const int ProbeRange = 20',
    'public const int MaximumEligibleMp = LowMpRules.RecuperateCost',
    'public const int ObservationEnterThreshold = MaximumEligibleMp + 1',
    'public const long FocusEmptyStabilityMilliseconds = 100',
    'public const long MinimumWriteIntervalMilliseconds = 1_000',
    'if (observation.HardReset)',
    'if (!observation.ConfigurationEnabled)',
    'if (!observation.IsCrystallineConflict)',
    'var next = ObserveConfirmedFocusDrift(state, observation)',
    'if (next.ManualOverrideLatched)',
    'if (!observation.LocalPlayerExactAndAlive || !observation.LocalPlayer.IsValid)',
    'if (!observation.MetadataVerified)',
    'if (!observation.TextInputStateKnown)',
    'if (observation.TextInputActive)',
    'if (observation.FocusState == AutoLowMpFocusObservedState.Unknown)',
    'if (!observation.CompleteCanonicalEnemySet ||',
    '!HasCompleteExactCanonicalSet(observation.Candidates))',
    'var lowMpWaveActive = observation.Candidates.Any(IsLowMpWaveMember)',
    'LowMpWaveActive = false',
    'AttemptSpentForWave = false',
    'AttemptSpentForWave = observation.FocusState != AutoLowMpFocusObservedState.Empty',
    'if (observation.FocusState == AutoLowMpFocusObservedState.Occupied)',
    'if (next.AttemptSpentForWave)',
    'if (!HasStableEmptyFocus(next, observation.NowMilliseconds))',
    'if (!CanIssueWrite(next.LastAttemptAtMilliseconds, observation.NowMilliseconds))',
    'var selectedIndex = SelectBestCandidateIndex(observation.Candidates, observation.LocalPlayer)',
    'AttemptSpentForWave = true',
    'LastAttemptAtMilliseconds = observation.NowMilliseconds',
    'AutoLowMpFocusTargetDecisionKind.SetFocus',
    'outcome == AutoLowMpFocusTargetSetOutcome.ExactReadbackConfirmed && intent.IsValid',
    'state with { LastConfirmedFocusTarget = intent.Target }',
    'candidates.Count != EnemySlotRules.LastSlot',
    '!slots.Add(candidate.EnemySlot)',
    '!gameObjectIds.Add(candidate.Actor.GameObjectId)',
    '!entityIds.Add(candidate.Actor.EntityId)',
    'candidate.CurrentMp <= MaximumEligibleMp',
    'candidate.CurrentMp <= candidate.MaximumMp',
    'candidate.NativeTargetValid',
    'candidate.NativeRangeAndLineOfSight',
    'focusState == AutoLowMpFocusObservedState.Empty',
    'intent.LocalPlayer == currentLocalPlayer',
    'intent.EnemySlot == candidate.EnemySlot',
    'intent.Target.Equals(candidate.Actor)',
    'ManualOverrideLatched = true',
    'LastConfirmedFocusTarget = default'
) 'Pure Auto Low-MP Focus exact wave, empty-focus, and frozen-intent rules'
if ($autoLowMpFocusTargetRules -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|HookFromAddress|ITargetManager|TargetManager|SetTarget|RaptureShellModule|ExecuteCommandInner|MarkingController|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Auto Low-MP Focus Core must remain pure and cannot read time, native state, input, targets, or action/command boundaries.'
}
if ($normalizedAutoLowMpFocusTargetRules -notmatch 'if \(observation\.HardReset\).*?HardReset.*?if \(!observation\.ConfigurationEnabled\).*?ConfigurationDisabled.*?if \(!observation\.IsCrystallineConflict\).*?NotCrystallineConflict.*?var next = ObserveConfirmedFocusDrift\(state, observation\).*?if \(next\.ManualOverrideLatched\).*?ManualOverrideLatched.*?if \(!observation\.LocalPlayerExactAndAlive \|\| !observation\.LocalPlayer\.IsValid\).*?LocalPlayerInvalid.*?if \(!observation\.MetadataVerified\).*?MetadataUnverified.*?if \(!observation\.TextInputStateKnown\).*?TextInputStateUnknown.*?if \(observation\.TextInputActive\).*?TextInputActive.*?if \(observation\.FocusState == AutoLowMpFocusObservedState\.Unknown\).*?FocusStateUnknown.*?if \(!observation\.CompleteCanonicalEnemySet \|\| !HasCompleteExactCanonicalSet\(observation\.Candidates\)\).*?CanonicalEnemySetIncomplete' -or
    $normalizedAutoLowMpFocusTargetRules -notmatch 'if \(!lowMpWaveActive\).*?LowMpWaveActive = false, AttemptSpentForWave = false.*?NoTrustedLowMpWave.*?if \(!next\.LowMpWaveActive\).*?LowMpWaveActive = true, AttemptSpentForWave = observation\.FocusState != AutoLowMpFocusObservedState\.Empty.*?else if \(observation\.FocusState != AutoLowMpFocusObservedState\.Empty && !next\.AttemptSpentForWave\).*?AttemptSpentForWave = true.*?if \(observation\.FocusState == AutoLowMpFocusObservedState\.Occupied\).*?FocusOccupied.*?if \(next\.AttemptSpentForWave\).*?WaveAlreadySpent' -or
    $normalizedAutoLowMpFocusTargetRules -notmatch 'var spent = next with \{ AttemptSpentForWave = true, LastAttemptAtMilliseconds = observation\.NowMilliseconds.*?return new AutoLowMpFocusTargetDecision\( spent, AutoLowMpFocusTargetDecisionKind\.SetFocus, AutoLowMpFocusTargetDecisionReason\.ReadyToSet') {
    throw 'Auto Low-MP Focus must gate exact CC before one trusted wave, preserve spent state through occupancy/unknowns, and spend/rate-limit before publishing one setter intent.'
}
if ($normalizedAutoLowMpFocusTargetRules -notmatch 'if \(!state\.LastConfirmedFocusTarget\.IsValid \|\| observation\.FocusState == AutoLowMpFocusObservedState\.Unknown\).*?return state;.*?if \(observation\.FocusState == AutoLowMpFocusObservedState\.Occupied && observation\.FocusTarget\.Equals\(state\.LastConfirmedFocusTarget\)\).*?return state;.*?ManualOverrideLatched = true, LastConfirmedFocusTarget = default' -or
    $normalizedAutoLowMpFocusTargetRules -notmatch 'state\.FocusEmptySinceMilliseconds >= 0 && nowMilliseconds >= state\.FocusEmptySinceMilliseconds.*?nowMilliseconds - state\.FocusEmptySinceMilliseconds >= FocusEmptyStabilityMilliseconds' -or
    $normalizedAutoLowMpFocusTargetRules -notmatch 'lastAttemptAtMilliseconds < 0 \|\| \(nowMilliseconds >= lastAttemptAtMilliseconds && nowMilliseconds - lastAttemptAtMilliseconds >= MinimumWriteIntervalMilliseconds\)') {
    throw 'Confirmed Focus drift must latch automation off, while empty-focus stability and the one-second write interval use only caller-provided monotonic time.'
}

$autoLowMpFocusTestMethods = @(
    'CanonicalSetAndEligibilityAreStrict',
    'RankingIsMpThenHpThenStableIdentity',
    'InclusiveThresholdUsesAnIndependentTrustedLatch',
    'EmptyFocusMustBeStableAndWaveIsOneShot',
    'OccupiedFocusSpendsWaveWithoutDelayedMutation',
    'ASeparatedWaveCanRearmWithoutRetryingFailure',
    'IntermediateMpCannotRearmASpentWave',
    'UnknownMpCannotRearmASpentWave',
    'ConfirmedFocusDriftLatchesUntilExplicitReset',
    'FrozenIntentRequiresEveryFinalGate'
)
foreach ($method in $autoLowMpFocusTestMethods) {
    Assert-Literals $autoLowMpFocusTargetSelfTests @("public static void $method()") "Auto Low-MP Focus self-test $method"
    Assert-Literals $autoLowMpFocusTargetProgram @("AutoLowMpFocusTargetSelfTests.$method") "Auto Low-MP Focus test registration $method"
}
Assert-Literals $autoLowMpFocusTargetSelfTests @(
    'exactly 2000 MP is eligible for Auto Focus',
    '2001 MP is above the Auto Focus entry boundary',
    'Auto Focus enters after a trusted stable exact-2000 sample',
    '2001 cannot enter the independent Auto Focus latch',
    'the independent latch retains the existing 2300 exit boundary',
    '99 ms is below the empty-focus boundary',
    '100 ms of empty focus allows one exact intent',
    'the wave is spent before runtime mutation',
    'the same continuous low-MP wave cannot retry',
    'occupied focus consumes the old wave',
    'clearing a manual focus cannot trigger a delayed set',
    'terminal setter failure never retries the wave',
    'a later distinct low-MP wave can emit one new intent',
    '2001 through 2299 remains inside the exit-hysteresis wave',
    'unknown telemetry preserves an established low-MP wave',
    'manual, game, or external clear latches automation off',
    'MP recovery above 2000 at the boundary cancels the spent intent',
    'a focus appearing at the boundary blocks without overwrite',
    'frozen identity drift cannot choose an alternate'
) 'Auto Low-MP Focus canonical, hysteresis, one-wave, drift, and final-gate tests'
if ([regex]::Matches($autoLowMpFocusTargetProgram, '\bAutoLowMpFocusTargetSelfTests\.\w+').Count -ne $autoLowMpFocusTestMethods.Count) {
    throw 'All ten Auto Low-MP Focus safety tests must remain registered exactly once.'
}

Assert-Literals $autoLowMpFocusTargetService @(
    'private const long UpdateIntervalMilliseconds = 100',
    'private readonly ITargetManager targetManager',
    'metadata.RecuperateVerified && metadata.AutoLowMpFocusProbeVerified',
    'framework.Update += OnFrameworkUpdate',
    'if (started) framework.Update -= OnFrameworkUpdate',
    'ResetInternal("Disposed without changing Focus Target")',
    'configuration.Enabled && configuration.EnableAutoLowMpFocusTarget',
    'context == SupportedPvPContext.CrystallineConflict',
    'clientState.TerritoryType != activeTerritory',
    '(wasCrystallineConflict && !isCrystallineConflict)',
    '(!wasCrystallineConflict && isCrystallineConflict)',
    '(wasConfigured && !configured)',
    'ResolveExactCandidates(',
    'state = decision.State',
    'setterIntentCount++',
    'TrySetFrozenIntentOnce(',
    'AutoLowMpFocusTargetRules.ApplySetOutcome(state, intent, outcome)',
    'diagnosticsBefore.SlotCapacity != EnemySlotRules.LastSlot',
    'diagnosticsBefore.ResolvedSlots != EnemySlotRules.LastSlot',
    '!ReferenceEquals(diagnosticsBefore, executeTracker.Diagnostics)',
    '!ReferenceEquals(trackerEnemies, executeTracker.Enemies)',
    'snapshotSlots.Add(snapshot.Slot)',
    'snapshotGameObjectIds.Add(snapshot.GameObjectId)',
    'snapshotEntityIds.Add(snapshot.EntityId)',
    'nativeGameObjectIds.Add(player.GameObjectId)',
    'nativeEntityIds.Add(player.EntityId)',
    'nativeAddresses.Add(player.Address)',
    'snapshot.GameObjectId != player.GameObjectId',
    'snapshot.EntityId != player.EntityId',
    'enterThreshold: AutoLowMpFocusTargetRules.ObservationEnterThreshold',
    'exitThreshold: LowMpRules.ExitThreshold',
    'nextLowMp.HasTrustedSample && nextLowMp.IsUnavailable',
    'player.CurrentMp <= AutoLowMpFocusTargetRules.MaximumEligibleMp',
    'SeitonReadinessProbe.HasRangeAndLineOfSight(',
    'AutoLowMpFocusTargetRules.ProbeActionId',
    'stablePlayer!.Address != player.Address',
    'stablePlayer.GameObjectId != player.GameObjectId',
    'stablePlayer.EntityId != player.EntityId',
    'AutoLowMpFocusTargetRules.HasCompleteExactCanonicalSet(candidates)',
    '!TryGetTextInputState(out var textInputActive) || textInputActive',
    '!TryReadFocus(out var firstFocusState, out _)',
    'firstFocusState != AutoLowMpFocusObservedState.Empty',
    'TryResolveFrozenCandidate(',
    'AutoLowMpFocusTargetRules.CanSetFrozenIntent(',
    '!TryReadFocus(out var finalFocusState, out _)',
    'finalFocusState != AutoLowMpFocusObservedState.Empty',
    'setterInvoked = true',
    'targetManager.FocusTarget = exactTarget',
    'readbackState == AutoLowMpFocusObservedState.Occupied',
    'readbackTarget == intent.Target',
    'setter invoked without exact readback; no retry',
    'target.GameObjectId != intent.Target.GameObjectId',
    'target.EntityId != intent.Target.EntityId',
    '!lowMpStates.TryGetValue(intent.Target, out var lowMpState)',
    'exactTarget = target',
    'tablePlayer.Address != resolved.Address',
    'tablePlayer.GameObjectId != resolved.GameObjectId',
    'tablePlayer.EntityId != resolved.EntityId',
    'var focus = targetManager.FocusTarget',
    'configuration.EnableWolvesDenTesting',
    'private void ResetLowMpSampling() => lowMpStates.Clear()',
    'state = AutoLowMpFocusTargetState.Initial'
) 'Auto Low-MP Focus coherent S1-S5 capture, trusted MP, empty-focus, and sole exact setter runtime'
$autoLowMpFocusWithoutReviewedSetter = $autoLowMpFocusTargetService -replace '(?m)^\s*targetManager\.FocusTarget\s*=\s*exactTarget;\s*$', ''
if ($autoLowMpFocusWithoutReviewedSetter -match '\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=' -or
    $autoLowMpFocusTargetService -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|Hook<|HookFromAddress|IGameInteropProvider|SignatureAttribute|SigScanner|RaptureShellModule|ExecuteCommandInner|MarkingController|SetTarget|SetFocusTarget|SetSoftTarget|SetMouseOverTarget|SetGPoseTarget)\b') {
    throw 'Auto Low-MP Focus may only perform its one reviewed managed FocusTarget write; it owns no action, hook, command, marker, hard/soft/mouse-over/GPose, or native target boundary.'
}
if ([regex]::Matches($autoLowMpFocusTargetService, '\bTrySetFrozenIntentOnce\s*\(').Count -ne 2 -or
    [regex]::Matches($autoLowMpFocusTargetService, '\btargetManager\.FocusTarget\s*=').Count -ne 1 -or
    [regex]::Matches($autoLowMpFocusTargetService, '\bTryReadFocus\s*\(').Count -lt 4) {
    throw 'Auto Low-MP Focus must have one setter call site/definition, one FocusTarget assignment, and repeated fail-closed focus reads.'
}
$autoLowMpFocusStateStoredIndex = $normalizedAutoLowMpFocusTargetService.IndexOf('state = decision.State;')
$autoLowMpFocusIntentCountIndex = $normalizedAutoLowMpFocusTargetService.IndexOf('setterIntentCount++;')
$autoLowMpFocusAttemptIndex = $normalizedAutoLowMpFocusTargetService.IndexOf('var outcome = TrySetFrozenIntentOnce(')
if ($autoLowMpFocusStateStoredIndex -lt 0 -or
    $autoLowMpFocusIntentCountIndex -le $autoLowMpFocusStateStoredIndex -or
    $autoLowMpFocusAttemptIndex -le $autoLowMpFocusIntentCountIndex) {
    throw 'Core spent/rate state must be stored before Auto Low-MP Focus records and crosses its one setter boundary.'
}
$autoLowMpFocusSetterBody = [regex]::Match(
    $normalizedAutoLowMpFocusTargetService,
    'private AutoLowMpFocusTargetSetOutcome TrySetFrozenIntentOnce\(.*?private bool TryResolveFrozenCandidate\(').Value
if ([string]::IsNullOrWhiteSpace($autoLowMpFocusSetterBody) -or
    $autoLowMpFocusSetterBody -notmatch 'TryReadFocus\(out var firstFocusState, out _\).*?firstFocusState != AutoLowMpFocusObservedState\.Empty.*?TryResolveFrozenCandidate\(.*?CanSetFrozenIntent\(.*?TryReadFocus\(out var finalFocusState, out _\).*?finalFocusState != AutoLowMpFocusObservedState\.Empty.*?setterInvoked = true; targetManager\.FocusTarget = exactTarget;.*?TryReadFocus\(out var readbackState, out var readbackTarget\).*?readbackTarget == intent\.Target' -or
    $autoLowMpFocusSetterBody -cmatch '\b(foreach|for|while|SelectBestCandidateIndex|Clear|Restore|Alternate|Retry)\b') {
    throw 'The sole Focus setter must double-read empty state, revalidate only the frozen actor, set once, and use readback only for confirmation without loops, restore, alternate, or retry.'
}
$autoLowMpFocusDisposeBody = [regex]::Match(
    $normalizedAutoLowMpFocusTargetService,
    'public void Dispose\(\).*?private void OnFrameworkUpdate\(').Value
$autoLowMpFocusResetBody = [regex]::Match(
    $normalizedAutoLowMpFocusTargetService,
    'private void ResetInternal\(string reason\).*?private void Publish\(').Value
if ([string]::IsNullOrWhiteSpace($autoLowMpFocusDisposeBody) -or
    [string]::IsNullOrWhiteSpace($autoLowMpFocusResetBody) -or
    $autoLowMpFocusDisposeBody -match '\.FocusTarget\s*=|TrySetFrozenIntentOnce' -or
    $autoLowMpFocusResetBody -match '\.FocusTarget\s*=|TrySetFrozenIntentOnce') {
    throw 'Auto Low-MP Focus dispose/reset may clear only local state and must never clear, restore, replace, or set FFXIV Focus Target.'
}

$autoLowMpFocusMetadata = Read-RequiredSource (Join-Path $pluginServicesRoot 'SeitonMetadataGuard.cs') 'Auto Low-MP Focus metadata guard'
Assert-Literals $autoLowMpFocusMetadata @(
    'bool AutoLowMpFocusProbeVerified',
    'ValidateFeature("Auto Low-MP Focus probe"',
    'actions.TryGetRow(AutoLowMpFocusTargetRules.ProbeActionId, out var action)',
    'action.Name.ToString() == "Seiton Tenchu"',
    'action.Icon == EnemyCombatConstants.SeitonIconId',
    'action.IsPvP',
    'action.IsPlayerAction',
    'action.ClassJob.IsValid',
    'action.ClassJob.RowId == 30',
    'action.Range == AutoLowMpFocusTargetRules.ProbeRange',
    'action.EffectRange == 0',
    'action.CanTargetHostile',
    '!action.CanTargetSelf',
    '!action.CanTargetParty',
    '!action.CanTargetAlly',
    '!action.TargetArea',
    'action.RequiresLineOfSight',
    'autoLowMpFocusProbeVerified,'
) 'Auto Low-MP Focus read-only 20-yalm hostile probe metadata'
if ([regex]::Matches($autoLowMpFocusMetadata, '\bValidateFeature\("Auto Low-MP Focus probe"').Count -ne 1) {
    throw 'Auto Low-MP Focus must have exactly one independently fail-closed metadata feature gate.'
}

$autoLowMpFocusReadiness = Read-RequiredSource $readinessPath 'Auto Low-MP Focus native range probe'
Assert-Literals $autoLowMpFocusReadiness @(
    'internal const uint BaseActionId = 29515',
    'internal const float MaximumRange = 20f',
    'if (resolvedActionId is not (BaseActionId or FollowUpActionId)) return false',
    'ActionManager.GetActionInRangeOrLoS(',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeStatus)'
) 'Shared native 20-yalm range/line-of-sight probe'

Assert-Literals $pluginSource @(
    'private readonly AutoLowMpFocusTargetService autoLowMpFocusTarget',
    'autoLowMpFocusTarget = new AutoLowMpFocusTargetService(',
    'autoLowMpFocusTarget.Start()',
    'autoLowMpFocusTarget.Dispose()',
    'auto-low-mp-focus[{autoLowMpFocusTarget.Diagnostics.ToChatLine()}]'
) 'Auto Low-MP Focus construction, lifecycle, and diagnostics'
$autoLowMpFocusTypeReferences = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bAutoLowMpFocusTargetService\b')
if ($autoLowMpFocusTypeReferences.Count -ne 4 -or
    @($autoLowMpFocusTypeReferences | Where-Object { $_.Path -notin @($pluginPath, $autoLowMpFocusTargetServicePath) }).Count -ne 0 -or
    $targetHighlight -match '\bAutoLowMpFocusTargetService\b') {
    throw 'Auto Low-MP Focus runtime may be owned only by Plugin.cs; renderers remain read-only observers and cannot acquire the mutation service.'
}

# DRK Shadowbringer macro assistance is default-off and may arm only from the
# exact /seitonbringer macro line immediately followed by a native PvP
# Souleater-combo call. CC keeps its canonical S1-S5 resolution. The separately
# opted-in Wolves' Den test path accepts only the exact current native hard-
# targeted striking dummy and owns no synthetic enemy-slot or duel-opponent
# fallback. A proven native 2.40-second combo-GCD restart opens one cycle.
# Unknown observations preserve that cycle and its spent ownership; only a
# later exact restart plus changed LastUsedActionSequence opens another.
$darkKnightShadowbringerRules = Read-RequiredSource $darkKnightShadowbringerRulesPath 'DRK Shadowbringer macro rules'
$normalizedDarkKnightShadowbringerRules = $darkKnightShadowbringerRules -replace '\s+', ' '
$darkKnightShadowbringerService = Read-RequiredSource $darkKnightShadowbringerServicePath 'DRK Shadowbringer macro runtime'
$normalizedDarkKnightShadowbringerService = $darkKnightShadowbringerService -replace '\s+', ' '
$darkKnightShadowbringerSelfTests = Read-RequiredSource $darkKnightShadowbringerSelfTestsPath 'DRK Shadowbringer macro self-tests'
$darkKnightShadowbringerProgram = Read-RequiredSource (
    Join-Path $coreSelfTestRoot 'Program.cs') 'Core self-test registration'

Assert-Literals $darkKnightShadowbringerRules @(
    'public const uint DarkKnightJobId = 32',
    'public const uint DarkKnightClassJobCategoryId = 98',
    'public const uint HardSlashActionId = 29085',
    'public const uint SyphonStrikeActionId = 29086',
    'public const uint SouleaterActionId = 29087',
    'public const uint ScarletDeliriumActionId = 41434',
    'public const uint ComeuppanceActionId = 41435',
    'public const uint TorcleaverActionId = 41436',
    'public const uint SouleaterComboRouteId = 52',
    'public const uint ShadowbringerActionId = 29091',
    'public const uint DarkArtsShadowbringerActionId = 29738',
    'public const uint DeliriumStatusId = 3033',
    'public const uint DarkArtsStatusId = 3034',
    'public const uint ShadowbringerIconId = 9594',
    'public const uint DarkArtsStatusIconId = 213107',
    'public const uint ShadowbringerHpCost = 12000',
    'public const uint WolvesDenStrikingDummyNameId = 541',
    'public const byte StandardComboSecondaryCostType = 58',
    'public const byte DeliriumComboSecondaryCostType = 147',
    'public const int ComboRecastGroupIndex = 57',
    'public const int ShadowbringerRecastGroupIndex = 0',
    'public const int ComboAdjustedRecastMilliseconds = 2400',
    'public const int ShadowbringerAdjustedRecastMilliseconds = 1000',
    'public const float ComboTotalToleranceSeconds = 0.01f',
    'public const float CycleResetEpsilonSeconds = 0.025f',
    'public const float MinimumNoClipRemainingSeconds = 0.6f',
    'public const float MaximumNoClipRemainingSeconds = 0.8f',
    'public const int MacroTokenLifetimeMilliseconds = 750',
    'CurrentCycleToken',
    'SpentCycleToken',
    'public bool HasProvenCycle => CurrentCycleToken != 0',
    'HasProvenCycle && SpentCycleToken == CurrentCycleToken',
    'if (observation.HardReset)',
    'DarkKnightGcdCycleState.Initial',
    'if (!IsExactKnownCycleObservation(observation))',
    'state,',
    'DarkKnightGcdObservationOutcome.Unknown',
    'observation.ElapsedSeconds + CycleResetEpsilonSeconds <',
    'state.PreviousElapsedSeconds',
    'observation.LastUsedActionSequence != state.PreviousLastUsedActionSequence',
    'CurrentCycleToken = NextToken(state.CurrentCycleToken)',
    'expectedCycleToken == 0',
    'state.CurrentCycleToken != expectedCycleToken',
    'state.SpentCycleToken == expectedCycleToken',
    'spentState = state with { SpentCycleToken = expectedCycleToken }',
    'observation.NowMilliseconds >= arm.ExpiresAtMilliseconds',
    '!observation.MacroLocked',
    'string.Equals(observation.MacroName, arm.MacroName, StringComparison.Ordinal)',
    'arm.MacroLine is < 0 or >= 15',
    'observation.MacroLine != arm.MacroLine + 1',
    'observation.LocalAddress != arm.LocalAddress',
    'observation.CycleToken == 0 || observation.CycleToken != arm.CycleToken',
    'observation.ActionType != 1',
    'observation.UseActionMode is not (0 or 100)',
    'observation.ComboRouteId != SouleaterComboRouteId',
    '!IsComboCarrierAction(observation.RawActionId)',
    '!IsComboCarrierAction(observation.AdjustedActionId)',
    'observation.ExtraParam != 0',
    'remainingSeconds >= MinimumNoClipRemainingSeconds',
    'remainingSeconds <= MaximumNoClipRemainingSeconds',
    'DarkArtsShadowbringerActionId => hasDarkArts && currentHp > 0',
    'ShadowbringerActionId => !hasDarkArts && currentHp > ShadowbringerHpCost',
    'context == SupportedPvPContext.CrystallineConflict',
    'wolvesDenTestingEnabled && context == SupportedPvPContext.WolvesDen',
    'metadataVerified &&',
    'battleNpcCombatant &&',
    'nameId == WolvesDenStrikingDummyNameId',
    'nativeIdentityValid &&',
    '!isSelf &&',
    'aliveWithPositiveHp &&',
    'targetable',
    'recastGroupIndex == ComboRecastGroupIndex',
    'Math.Abs(totalSeconds - ComboAdjustedRecastMilliseconds / 1000f) <=',
    'adjustedRecastMilliseconds == ComboAdjustedRecastMilliseconds',
    'current == ulong.MaxValue ? 1UL : current + 1UL'
) 'Pure DRK macro IDs, cycle ownership, exact adjacent pairing, no-clip window, and resource gates'

if ($normalizedDarkKnightShadowbringerRules -notmatch 'if \(observation\.HardReset\).*?DarkKnightGcdCycleState\.Initial.*?if \(!IsExactKnownCycleObservation\(observation\)\).*?state, DarkKnightGcdObservationOutcome\.Unknown.*?if \(!state\.HasPreviousKnownObservation\).*?DarkKnightGcdObservationOutcome\.Primed.*?var recastRestarted = observation\.IsActive && \(!state\.PreviousActive \|\| observation\.ElapsedSeconds \+ CycleResetEpsilonSeconds < state\.PreviousElapsedSeconds\); var exactNewActionSequence = observation\.LastUsedActionSequence != state\.PreviousLastUsedActionSequence; if \(!recastRestarted \|\| !exactNewActionSequence\).*?DarkKnightGcdObservationOutcome\.Unchanged.*?CurrentCycleToken = NextToken\(state\.CurrentCycleToken\).*?DarkKnightGcdObservationOutcome\.OpenedCycle' -or
    $normalizedDarkKnightShadowbringerRules -notmatch 'if \(expectedCycleToken == 0 \|\| state\.CurrentCycleToken != expectedCycleToken \|\| state\.SpentCycleToken == expectedCycleToken\).*?return false;.*?spentState = state with \{ SpentCycleToken = expectedCycleToken \}; return true;' -or
    $normalizedDarkKnightShadowbringerRules -notmatch 'if \(!observation\.PluginEnabled \|\| !observation\.FeatureEnabled\).*?Disabled.*?if \(!observation\.MetadataVerified\).*?MetadataMismatch.*?if \(!observation\.ExactSupportedContext\).*?InvalidContext.*?if \(!observation\.SafeCarrierPath\).*?UnsafeCarrierPath.*?if \(!observation\.ExactCycleSnapshot \|\| !observation\.CycleActive.*?CycleUnknownOrChanged.*?if \(observation\.SpentCycleToken == observation\.ExpectedCycleToken && !observation\.CycleOwnedByThisAttempt\).*?CycleAlreadySpent.*?if \(!IsWithinNoClipWeaveWindow\(observation\.RemainingGcdSeconds\)\).*?OutsideNoClipWindow.*?if \(!observation\.NativeQueueClearAndStable\).*?NativeQueueOwned.*?if \(!observation\.ActionSequenceStable\).*?ActionSequenceChanged.*?if \(!observation\.AnimationLockClear\).*?AnimationLocked.*?if \(!observation\.NotCasting\).*?Casting.*?if \(!observation\.OwnGuardClear\).*?OwnGuardActiveOrPropagating.*?if \(!observation\.TargetIdentityStable \|\| !observation\.TargetAliveAndTargetable\).*?InvalidTarget.*?if \(!observation\.TargetGuardClear\).*?TargetGuardActive.*?if \(!observation\.ComboHasNativeRangeAndLineOfSight\).*?ComboOutOfRangeOrLineOfSight.*?if \(!observation\.ShadowbringerHasNativeRangeAndLineOfSight\).*?ShadowbringerOutOfRangeOrLineOfSight.*?if \(!observation\.ComboStructurallyReady\).*?ComboStructurallyUnavailable.*?IsShadowbringerResourceStateValid.*?InvalidShadowbringerResourceState.*?if \(!observation\.ShadowbringerCooldownReady \|\| !observation\.ShadowbringerActionReady \|\| !observation\.ShadowbringerResourcesReady\).*?ShadowbringerUnavailable.*?ShouldAttempt: true' -or
    $normalizedDarkKnightShadowbringerRules -notmatch 'public static bool CanExecuteInContext\( SupportedPvPContext context, bool wolvesDenTestingEnabled\) => context == SupportedPvPContext\.CrystallineConflict \|\| \(wolvesDenTestingEnabled && context == SupportedPvPContext\.WolvesDen\);' -or
    $normalizedDarkKnightShadowbringerRules -notmatch 'public static bool IsExactWolvesDenStrikingDummy\( bool metadataVerified, bool battleNpcCombatant, uint nameId, bool nativeIdentityValid, bool isSelf, bool aliveWithPositiveHp, bool targetable\) => metadataVerified && battleNpcCombatant && nameId == WolvesDenStrikingDummyNameId && nativeIdentityValid && !isSelf && aliveWithPositiveHp && targetable;') {
    throw 'DRK Core must preserve unknown cycle ownership, spend atomically, and require every final context/queue/lock/Guard/target/range/resource gate before one attempt.'
}
if ($darkKnightShadowbringerRules -cmatch '\b(Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread|ActionManager|ObjectTable|RaptureShell|UseAction|Dispatch|Retry|Replay|ITargetManager|TargetManager|SetTarget)\b') {
    throw 'Pure DRK macro rules must own no clock, native API, target service, action call, retry, or replay.'
}

$darkKnightShadowbringerTestMethods = @(
    'ExactIdsAndCarrierSetArePinned',
    'CycleRequiresAProvenExactReset',
    'UnknownPreservesSpentOwnershipAndNextResetRearms',
    'CycleTokenWrapsToOne',
    'PairingRequiresImmediateQueueableSouleaterLine',
    'NoClipWindowIsInclusiveAndNeverLate',
    'HpAndDarkArtsGateIsExact',
    'AttemptRequiresEverySafetyGate'
)
foreach ($method in $darkKnightShadowbringerTestMethods) {
    Assert-Literals $darkKnightShadowbringerSelfTests @("public static void $method()") "DRK Shadowbringer self-test $method"
    Assert-Literals $darkKnightShadowbringerProgram @("DarkKnightShadowbringerMacroSelfTests.$method") "DRK Shadowbringer test registration $method"
}
if ([regex]::Matches($darkKnightShadowbringerSelfTests, '(?m)^\s*public static void\s+\w+\s*\(').Count -ne $darkKnightShadowbringerTestMethods.Count -or
    [regex]::Matches($darkKnightShadowbringerProgram, '\bDarkKnightShadowbringerMacroSelfTests\.\w+').Count -ne $darkKnightShadowbringerTestMethods.Count) {
    throw 'The DRK Shadowbringer Core test suite and registration must stay at the exact reviewed eight safety cases.'
}
Assert-Literals $darkKnightShadowbringerSelfTests @(
    'False(DarkKnightShadowbringerMacroRules.TrySpendCycle(unknown.State, 1, out _)',
    'True(DarkKnightShadowbringerMacroRules.TrySpendCycle(next, 2, out _)',
    'arm with { MacroLine = 0 }',
    'valid with { MacroLine = 1 }',
    'valid with { UseActionMode = 2 }',
    'valid with { UseActionMode = 1 }',
    'valid with { MacroLine = 3 }',
    'valid with { ComboRouteId = 0 }',
    'valid with { NowMilliseconds = 1_750 }',
    'IsWithinNoClipWeaveWindow(0.5f)',
    'IsWithinNoClipWeaveWindow(0.599f)',
    'IsWithinNoClipWeaveWindow(0.6f)',
    'IsWithinNoClipWeaveWindow(0.8f)',
    'IsWithinNoClipWeaveWindow(0.801f)',
    'IsShadowbringerResourceStateValid(29091, false, 12000)',
    'IsShadowbringerResourceStateValid(29091, false, 12001)',
    'IsShadowbringerResourceStateValid(29738, true, 1)',
    'valid with { NativeQueueClearAndStable = false }',
    'valid with { AnimationLockClear = false }',
    'valid with { OwnGuardClear = false }',
    'valid with { TargetGuardClear = false }',
    'valid with { ComboHasNativeRangeAndLineOfSight = false }',
    'valid with { ShadowbringerHasNativeRangeAndLineOfSight = false }',
    'CanExecuteInContext(',
    'SupportedPvPContext.CrystallineConflict,',
    'wolvesDenTestingEnabled: false',
    'SupportedPvPContext.WolvesDen,',
    'wolvesDenTestingEnabled: true',
    'SupportedPvPContext.None,',
    'IsExactWolvesDenStrikingDummy(',
    'nameId: 541',
    'nameId: 13078',
    'battleNpcCombatant: false',
    'metadataVerified: false',
    'isSelf: true',
    'valid with { ExactSupportedContext = false }'
) 'DRK tests for spent unknown state, exact pair modes/line, inclusive 600-800ms window, HP, queue, Guard, and dual native reachability'

Assert-Literals $darkKnightShadowbringerService @(
    'internal const string Command = "/seitonbringer"',
    'private const ulong InvalidObjectId = 0xE0000000',
    'private const float AnimationLockEpsilonSeconds = 0.0005f',
    'metadataVerified = ValidateMetadata(dataManager, log)',
    'wolvesDenDummyMetadataVerified = ValidateWolvesDenDummyMetadata(dataManager, log)',
    'framework.Update += OnFrameworkUpdate',
    'if (!string.IsNullOrWhiteSpace(arguments))',
    '!configuration.Enabled || !configuration.EnableDarkKnightShadowbringerMacro',
    'if (!hookAvailable)',
    'if (!metadataVerified)',
    'var shell = RaptureShellModule.Instance()',
    '!shell->MacroLocked',
    'shell->MacroCurrentLine is < 0 or >= 15',
    'shell->MacroLineText.ToString().Trim()',
    'StringComparison.OrdinalIgnoreCase',
    'DarkKnightShadowbringerMacroRules.CanExecuteInContext(',
    'configuration.EnableWolvesDenTesting',
    'context == SupportedPvPContext.WolvesDen && !wolvesDenDummyMetadataVerified',
    'DarkKnightShadowbringerArmOutcome.WolvesDenDummyMetadataMismatch',
    'if (!IsExactLocalDarkKnight(local))',
    'if (!currentCycle.HasProvenCycle)',
    'if (currentCycle.CurrentCycleSpent)',
    'SaturatingAdd(now, DarkKnightShadowbringerMacroRules.MacroTokenLifetimeMilliseconds)',
    'clientState.TerritoryType',
    'local!.GameObjectId',
    'local.EntityId',
    'local.Address',
    'currentCycle.CurrentCycleToken',
    'armedMacro = null',
    'DarkKnightShadowbringerMacroRules.EvaluatePair(',
    'CcImmunityBrakeTargetRules.IsDefaultTargetCarrier(targetId)',
    'GetNativeHardTargetId(local)',
    'CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(',
    'TryResolveExactTarget(',
    'TryResolveExactCanonicalEnemy(',
    'TryResolveExactWolvesDenHardTarget(',
    'usedDefaultTargetCarrier || context == SupportedPvPContext.WolvesDen',
    'target.GameObjectId',
    'target.EntityId',
    'target.Address',
    'target.ObjectKind',
    'target.SubKind',
    'target.NameId',
    'DarkKnightShadowbringerMacroRules.TrySpendCycle(',
    'cycleState = spentState',
    'cycleOwnedByThisAttempt: true',
    'accepted = dispatch()',
    'will not retry this GCD',
    'framework.Update -= OnFrameworkUpdate',
    'cycleState = DarkKnightGcdCycleState.Initial',
    'DarkKnightShadowbringerMacroRules.DarkKnightJobId',
    'actionManager->LastUsedActionSequence',
    'actionManager->ActionQueued',
    'actionManager->AnimationLock',
    'actionManager->CastActionId == 0',
    'EnemyCombatConstants.GuardStatusId',
    'EnemyCombatConstants.GuardStatusAlternateId',
    'ActionManager.GetActionInRangeOrLoS(',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight(',
    'checkRecastActive: false',
    'checkRecastActive: true',
    'actionManager->CheckActionResources(',
    'DarkKnightShadowbringerMacroRules.ShadowbringerHpCost',
    'DarkKnightShadowbringerMacroRules.DarkArtsStatusId'
) 'DRK runtime macro provenance, exact CC/Den context and identity, one-cycle claim, native queue/lock/Guard/range/resource gates, and lifecycle'

if ([regex]::Matches($darkKnightShadowbringerService, '\bRaptureShellModule\.Instance\s*\(').Count -ne 2 -or
    $darkKnightShadowbringerService -match '\b(ExecuteCommandInner|Utf8String|GetRaptureShellModule|SetRawValue|FireCallback)\b' -or
    $darkKnightShadowbringerService -match '->\s*(?:ActionQueued|QueuedActionType|QueuedActionId|QueuedTargetId|QueuedExtraParam|QueueType|QueuedComboRouteId)\s*=' -or
    $darkKnightShadowbringerService -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=' -or
    $darkKnightShadowbringerService -match '(?:->|\.)UseAction\s*\(') {
    throw 'The DRK service may read two exact macro snapshots and native queue/target state, but must not write shell/queue/targets or own a native action call.'
}

$darkKnightStartMatch = [regex]::Match(
    $darkKnightShadowbringerService,
    '(?s)internal void Start\(\)\s*\{(?<Body>.*?)\r?\n    \}\r?\n\r?\n    internal DarkKnightShadowbringerArmResult Arm')
if (-not $darkKnightStartMatch.Success) {
    throw 'The DRK Start method could not be isolated for framework-thread review.'
}
$darkKnightStartBody = $darkKnightStartMatch.Groups['Body'].Value
if ($darkKnightStartBody -notmatch 'framework\.Update \+= OnFrameworkUpdate' -or
    $darkKnightStartBody -match '\b(ObserveCycleNow|ActionManager\.Instance|objectTable|LocalPlayer)\b') {
    throw 'DRK Start may only subscribe cycle observation; local/native GCD reads must begin on the framework update thread.'
}
if ([regex]::Matches($darkKnightShadowbringerService, 'private void OnFrameworkUpdate\(IFramework _\) => ObserveCycleNow\(ActionManager\.Instance\(\)\);').Count -ne 1) {
    throw 'DRK must perform its deferred initial/native cycle observation through exactly one framework-update callback.'
}

$darkKnightPairMatch = [regex]::Match(
    $darkKnightShadowbringerService,
    '(?s)internal bool TryConsumePairedCarrier\((?<Body>.*?)\r?\n    \}\r?\n\r?\n    internal bool TryAttemptOnce')
if (-not $darkKnightPairMatch.Success) {
    throw 'The DRK exact carrier-pair method could not be isolated.'
}
$darkKnightPair = $darkKnightPairMatch.Groups['Body'].Value
$normalizedDarkKnightPair = $darkKnightPair -replace '\s+', ' '
if ($normalizedDarkKnightPair -notmatch 'armedMacro = null;.*?EvaluatePair\(.*?var context = ResolveContext\(\);.*?!CanExecuteInContext\(context\).*?var usedDefaultTargetCarrier = CcImmunityBrakeTargetRules\.IsDefaultTargetCarrier\(targetId\);.*?var nativeHardTargetId = usedDefaultTargetCarrier \|\| context == SupportedPvPContext\.WolvesDen \? GetNativeHardTargetId\(local\) : 0;.*?ResolveEffectiveTargetId\( targetId, targetId, nativeHardTargetId\);.*?TryResolveExactTarget\( context, local, effectiveTargetId, nativeHardTargetId,.*?\(\(usedDefaultTargetCarrier \|\| context == SupportedPvPContext\.WolvesDen\) && GetNativeHardTargetId\(local\) != nativeHardTargetId\).*?new DarkKnightShadowbringerPairedCarrier\( arm, context, actionId, adjustedActionId, \(uint\)mode, comboRouteId, effectiveTargetId, usedDefaultTargetCarrier, nativeHardTargetId, enemySlot, target\.GameObjectId, target\.EntityId, target\.Address, target\.ObjectKind, target\.SubKind, target\.NameId\)' -or
    $darkKnightPair -match '\b(WolvesDenOpponentResolver|PvPDuelManager)\b') {
    throw 'DRK pairing must consume the authored line once, require the exact supported context, capture the Den native hard target, and freeze every target identity field without a duel fallback.'
}

$darkKnightTargetRouterMatch = [regex]::Match(
    $darkKnightShadowbringerService,
    '(?s)private bool TryResolveExactTarget\((?<Body>.*?)\r?\n    \}\r?\n\r?\n    private bool TryResolveExactWolvesDenHardTarget')
$darkKnightDenResolverMatch = [regex]::Match(
    $darkKnightShadowbringerService,
    '(?s)private bool TryResolveExactWolvesDenHardTarget\((?<Body>.*?)\r?\n    \}\r?\n\r?\n    private bool TryResolveExactCanonicalEnemy')
$darkKnightCcResolverMatch = [regex]::Match(
    $darkKnightShadowbringerService,
    '(?s)private bool TryResolveExactCanonicalEnemy\((?<Body>.*?)\r?\n    \}\r?\n\r?\n    private SupportedPvPContext ResolveContext')
if (-not $darkKnightTargetRouterMatch.Success -or
    -not $darkKnightDenResolverMatch.Success -or
    -not $darkKnightCcResolverMatch.Success) {
    throw 'The DRK context router and exact CC/Den target resolvers could not be isolated.'
}
$darkKnightTargetRouter = $darkKnightTargetRouterMatch.Groups['Body'].Value
$darkKnightDenResolver = $darkKnightDenResolverMatch.Groups['Body'].Value
$darkKnightCcResolver = $darkKnightCcResolverMatch.Groups['Body'].Value
$normalizedDarkKnightTargetRouter = $darkKnightTargetRouter -replace '\s+', ' '
$normalizedDarkKnightDenResolver = $darkKnightDenResolver -replace '\s+', ' '
$normalizedDarkKnightCcResolver = $darkKnightCcResolver -replace '\s+', ' '
if ($normalizedDarkKnightTargetRouter -notmatch 'enemySlot = 0; if \(context == SupportedPvPContext\.CrystallineConflict\).*?TryResolveExactCanonicalEnemy\( localPlayer, targetId,.*?if \(context == SupportedPvPContext\.WolvesDen && CanExecuteInContext\(context\)\).*?TryResolveExactWolvesDenHardTarget\( localPlayer, targetId, nativeHardTargetId,' -or
    [regex]::Matches($darkKnightTargetRouter, '\bTryResolveExactCanonicalEnemy\s*\(').Count -ne 1 -or
    [regex]::Matches($darkKnightTargetRouter, '\bTryResolveExactWolvesDenHardTarget\s*\(').Count -ne 1) {
    throw 'DRK target routing must keep the canonical CC resolver and the exact Den dummy resolver as two explicit, non-fallback branches.'
}
if ($normalizedDarkKnightCcResolver -notmatch 'for \(var slot = EnemySlotRules\.FirstSlot; slot <= EnemySlotRules\.LastSlot; slot\+\+\).*?EnemySlotResolver\.Resolve\(objectTable, slot\).*?EnemySlotRules\.CanUseResolvedEnemy\(.*?if \(targetId == candidate\.GameObjectId \|\| targetId == candidate\.EntityId\).*?if \(matches\.Count != 1\).*?target = match\.Player; enemySlot = match\.Slot; resolution = "Exact canonical enemy"; return true;' -or
    $darkKnightCcResolver -match '\b(WolvesDen|WolvesDenOpponentResolver|PvPDuelManager|BNpcName|BattleNpcKind|NameId)\b') {
    throw 'The CC branch must remain the canonical unique S1-S5 resolver and must not inherit any Den dummy or duel-opponent logic.'
}
if ($normalizedDarkKnightDenResolver -notmatch 'if \(!IsNetworkObjectId\(nativeHardTargetId\)\) return false;.*?if \(!IsNetworkObjectId\(targetId\)\) return false;.*?SearchById\(nativeHardTargetId\).*?SearchByEntityId\(\(uint\)nativeHardTargetId\).*?!HasSameNativeIdentity\(byObjectId, byEntityId\).*?if \(!ActorIdMatches\(nativeHardTargetId, candidate!\)\) return false;.*?if \(!ActorIdMatches\(targetId, candidate!\)\) return false;.*?candidate is DalamudBattleNpc.*?BattleNpcKind: DalamudBattleNpcSubKind\.Combatant.*?candidate\.ObjectKind == DalamudObjectKind\.BattleNpc.*?IsExactWolvesDenStrikingDummy\( wolvesDenDummyMetadataVerified, battleNpcCombatant, candidate\.NameId, nativeIdentityValid: true, isSelf: isSelf, aliveWithPositiveHp: aliveWithPositiveHp, targetable: candidate\.IsTargetable\).*?SearchById\(candidate\.GameObjectId\).*?SearchByEntityId\(candidate\.EntityId\).*?!HasSameNativeIdentity\(candidate, canonicalByObjectId\).*?!HasSameNativeIdentity\(candidate, canonicalByEntityId\).*?GetNativeHardTargetId\(localPlayer\) != nativeHardTargetId.*?target = candidate;.*?return true;' -or
    $darkKnightDenResolver -match '\b(EnemySlotResolver|EnemySlotRules|WolvesDenOpponentResolver|PvPDuelManager|StatusFlags\.Hostile)\b') {
    throw 'The Den branch must accept only the exact current native hard-target NameId-541 combat BattleNpc and may not use S-slots, hostile-player, or duel-opponent fallback.'
}

$darkKnightAttemptMatch = [regex]::Match(
    $darkKnightShadowbringerService,
    '(?s)internal bool TryAttemptOnce\(.*?\r?\n    \}\r?\n\r?\n    public void Dispose')
if (-not $darkKnightAttemptMatch.Success) {
    throw 'The DRK one-attempt method could not be isolated for ownership review.'
}
$darkKnightAttempt = $darkKnightAttemptMatch.Value
$normalizedDarkKnightAttempt = $darkKnightAttempt -replace '\s+', ' '
if ([regex]::Matches($darkKnightAttempt, '\bdispatch\s*\(').Count -ne 1 -or
    $normalizedDarkKnightAttempt -notmatch 'var preliminary = CaptureAttempt\(.*?baseline: null, cycleOwnedByThisAttempt: false\);.*?EvaluateAttempt\( preliminary\.Observation\).*?if \(!preliminaryDecision\.ShouldAttempt\) return false;.*?TrySpendCycle\( cycleState, pairedCarrier\.Arm\.CycleToken, out var spentState\).*?cycleState = spentState; claimedCount\+\+;.*?var final = CaptureAttempt\(.*?preliminary, cycleOwnedByThisAttempt: true\);.*?EvaluateAttempt\(final\.Observation\).*?if \(!finalDecision\.ShouldAttempt\) return false;.*?Interlocked\.Increment\(ref attemptCount\);.*?accepted = dispatch\(\);.*?if \(accepted\) Interlocked\.Increment\(ref acceptedCount\);.*?return true;' -or
    $darkKnightAttempt -match '(?m)^\s*(?:for|foreach|while|do)\s*[({]' -or
    $darkKnightAttempt -cmatch '\b(Task|Timer|RetryAction|RetryDispatch|ReplayAction|FallbackAction|AlternateAction)\b') {
    throw 'DRK must claim the exact cycle before final rereads and issue one dispatch only; rejection, drift, or exception stays spent with no loop, retry, alternate, or fallback.'
}

if ($normalizedDarkKnightShadowbringerService -notmatch 'private void ObserveCycleNow\(ActionManager\* actionManager\).*?var context = ResolveContext\(\); if \(!CanExecuteInContext\(context\) \|\| !IsExactLocalDarkKnight\(local\).*?HardReset\("Context, player, job, or life changed"\).*?identityChanged = hasObservedLifetime && \(observedTerritoryId != clientState\.TerritoryType \|\| observedLocalGameObjectId != local!\.GameObjectId \|\| observedLocalEntityId != local\.EntityId \|\| observedLocalAddress != local\.Address\).*?GetRecastGroup\( \(int\)ActionType\.Action, DarkKnightShadowbringerMacroRules\.HardSlashActionId\).*?GetRecastGroupDetail\(groupIndex\).*?GetAdjustedRecastTime\( ActionType\.Action, DarkKnightShadowbringerMacroRules\.HardSlashActionId, true\).*?DarkKnightShadowbringerMacroRules\.ObserveCycle\(cycleState, observation\).*?cycleState = result\.State' -or
    $normalizedDarkKnightShadowbringerService -notmatch 'var queue = CaptureNativeQueue\(actionManager\); var sequence = actionManager == null \? \(ushort\)0 : actionManager->LastUsedActionSequence; var queueStable = !queue\.Active && \(baseline is null \|\| queue == baseline\.Value\.Queue\); var sequenceStable = baseline is null \|\| sequence == baseline\.Value\.Sequence;.*?animationLock <= AnimationLockEpsilonSeconds;.*?!local!\.IsCasting.*?actionManager->CastActionId == 0;' -or
    $normalizedDarkKnightShadowbringerService -notmatch 'var exactContext = context == carrier\.Context && CanExecuteInContext\(context\) && clientState\.TerritoryType == carrier\.Arm\.TerritoryId;.*?target\.GameObjectId == carrier\.TargetGameObjectId && target\.EntityId == carrier\.TargetEntityId && target\.Address == carrier\.TargetAddress && \(carrier\.Context != SupportedPvPContext\.WolvesDen \|\| target\.ObjectKind == carrier\.TargetObjectKind && target\.SubKind == carrier\.TargetSubKind && target\.NameId == carrier\.TargetNameId\) && \(!\(carrier\.UsedDefaultTargetCarrier \|\| carrier\.Context == SupportedPvPContext\.WolvesDen\) \|\| GetNativeHardTargetId\(local\) == carrier\.NativeHardTargetId\).*?HasActiveStatus\(target!, EnemyCombatConstants\.GuardStatusId\).*?HasActiveStatus\(target!, EnemyCombatConstants\.GuardStatusAlternateId\)' -or
    $normalizedDarkKnightShadowbringerService -notmatch 'currentAdjustedCarrier == carrier\.AdjustedActionId.*?GetActionInRangeOrLoS\( currentAdjustedCarrier, sourceObject, targetObject\).*?GetActionStatus\( ActionType\.Action, currentAdjustedCarrier, carrier\.EffectiveTargetId, checkRecastActive: false, checkCastingActive: true\) == 0.*?GetAdjustedActionId\( DarkKnightShadowbringerMacroRules\.ShadowbringerActionId\).*?GetActionInRangeOrLoS\( shadowAdjusted, sourceObject, targetObject\).*?shadowGroupIndex == DarkKnightShadowbringerMacroRules\.ShadowbringerRecastGroupIndex.*?!shadowDetail->IsActive.*?GetAdditionalRecastGroup\(ActionType\.Action, shadowAdjusted\) < 0.*?GetAdjustedRecastTime\( ActionType\.Action, shadowAdjusted, true\) == DarkKnightShadowbringerMacroRules\.ShadowbringerAdjustedRecastMilliseconds.*?GetActionStatus\( ActionType\.Action, shadowAdjusted, carrier\.EffectiveTargetId, checkRecastActive: true, checkCastingActive: true\) == 0.*?CheckActionResources\( ActionType\.Action, shadowAdjusted\) == 0') {
    throw 'DRK runtime must derive each supported-context cycle from exact native recast telemetry and repeat stable queue/sequence, frozen CC/Den identity, Guard, dual range/LoS, cooldown, readiness, and resource checks at the final boundary.'
}

if ($normalizedDarkKnightShadowbringerService -notmatch 'private static bool HasSameNativeIdentity\( DalamudGameObject\? left, DalamudGameObject\? right\) => HasValidNativeIdentity\(left\) && HasValidNativeIdentity\(right\) && left!\.GameObjectId == right!\.GameObjectId && left\.EntityId == right\.EntityId && left\.Address == right\.Address && left\.ObjectKind == right\.ObjectKind && left\.SubKind == right\.SubKind && \(left is not DalamudBattleChara leftBattleChara \|\| right is DalamudBattleChara rightBattleChara && leftBattleChara\.NameId == rightBattleChara\.NameId\);') {
    throw 'DRK canonical identity rereads must include game-object ID, entity ID, address, object kind, sub-kind, and battle-character NameId.'
}

Assert-Literals $darkKnightShadowbringerService @(
    'expectedSecondaryCostType: 0',
    'DarkKnightShadowbringerMacroRules.StandardComboSecondaryCostType',
    'DarkKnightShadowbringerMacroRules.DeliriumComboSecondaryCostType',
    'action.SecondaryCostType == expectedSecondaryCostType',
    'action.SecondaryCostValue.RowId == 0',
    '"Scarlet Delirium"',
    '9766',
    '"Comeuppance"',
    '9767',
    '"Torcleaver"',
    '9768',
    'primaryCostType: 105',
    'primaryCostValue: DarkKnightShadowbringerMacroRules.ShadowbringerHpCost',
    'isPlayerAction: false',
    'primaryCostType: 10',
    'primaryCostValue: DarkKnightShadowbringerMacroRules.DarkArtsStatusId',
    'route.Name.ToString() == "Souleater Combo"',
    'route.Action[0].RowId == DarkKnightShadowbringerMacroRules.HardSlashActionId',
    'route.Action[1].RowId == DarkKnightShadowbringerMacroRules.SyphonStrikeActionId',
    'route.Action[2].RowId == DarkKnightShadowbringerMacroRules.SouleaterActionId',
    'route.Unknown4',
    'darkArts.Name.ToString() == "Dark Arts"',
    'darkArts.StatusCategory == 1',
    '!darkArts.IsPermanent',
    '!darkArts.CanDispel',
    '!darkArts.LockMovement',
    'action.ActionCategory.RowId == 3',
    'action.CastType == 1',
    'action.Range == 5',
    'action.EffectRange == 0',
    'action.Recast100ms == 25',
    'action.CooldownGroup == 58',
    'action.ActionCategory.RowId == 4',
    'action.CastType == 4',
    'action.Range == 10',
    'action.EffectRange == 10',
    'action.Recast100ms == 10',
    'action.CooldownGroup == 1',
    'action.RequiresLineOfSight',
    'action.NeedToFaceTarget',
    'description.Contains("Consumes 12,000 HP when executed", StringComparison.Ordinal)',
    'description.Contains("Dark Arts", StringComparison.Ordinal)',
    'GetExcelSheet<BNpcName>(ClientLanguage.English)',
    'DarkKnightShadowbringerMacroRules.WolvesDenStrikingDummyNameId',
    'strikingDummy.Singular.ToString() == "striking dummy"',
    'strikingDummy.Plural.ToString() == "striking dummies"',
    'Crystalline Conflict DRK support remains available.'
) 'Current-sheet DRK combo, Shadowbringer, route, exact secondary costs, Dark Arts, and separate Den dummy metadata'
if ([regex]::Matches($darkKnightShadowbringerService, '\bIsExpectedComboAction\s*\(').Count -ne 7 -or
    [regex]::Matches($darkKnightShadowbringerService, '\bIsExpectedShadowbringer\s*\(').Count -ne 3 -or
    [regex]::Matches($darkKnightShadowbringerService, '\bValidateMetadata\s*\(').Count -ne 2 -or
    [regex]::Matches($darkKnightShadowbringerService, '\bValidateWolvesDenDummyMetadata\s*\(').Count -ne 2 -or
    [regex]::Matches($darkKnightShadowbringerService, 'expectedSecondaryCostType:\s*0').Count -ne 1 -or
    [regex]::Matches($darkKnightShadowbringerService, 'DarkKnightShadowbringerMacroRules\.StandardComboSecondaryCostType').Count -ne 2 -or
    [regex]::Matches($darkKnightShadowbringerService, 'DarkKnightShadowbringerMacroRules\.DeliriumComboSecondaryCostType').Count -ne 3) {
    throw 'DRK metadata must validate six combo rows with exact 0/58/58/147/147/147 secondary cost types, two Shadowbringer rows, one route, Dark Arts, and one separately cached Den dummy row.'
}

$darkKnightTypeReferences = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bDarkKnightShadowbringerMacroService\b')
if (@($darkKnightTypeReferences | Where-Object {
        $_.Path -notin @($pluginPath, $nearAssistPath, $darkKnightShadowbringerServicePath)
    }).Count -ne 0) {
    throw 'DRK Shadowbringer runtime may be owned only by Plugin.cs and consulted only by the existing Near Assist detour.'
}
Assert-Literals $pluginSource @(
    'private readonly DarkKnightShadowbringerMacroService darkKnightShadowbringer',
    'darkKnightShadowbringer = new DarkKnightShadowbringerMacroService(',
    'darkKnightShadowbringerCommandRegistered = commandManager.AddHandler(',
    'DarkKnightShadowbringerMacroService.Command',
    'new CommandInfo(OnDarkKnightShadowbringerCommand)',
    'AllowedInMacros = true',
    'Exact CC or enabled Wolves'' Den striking-dummy DRK helper: /seitonbringer, then',
    '/pvpac \"Souleater Combo\" <t>',
    'use the localized action name and ReAction Macro Queue + Turbo',
    '/seitonbringer arms only the immediately following authored DRK Souleater Combo <t> macro line in',
    'CC or enabled Wolves'' Den striking-dummy testing',
    '/seitonbringer is already owned by another plugin',
    'darkKnightShadowbringer.Start()',
    'if (darkKnightShadowbringerCommandRegistered)',
    'commandManager.RemoveHandler(DarkKnightShadowbringerMacroService.Command)',
    'nearAssist.Dispose()',
    'darkKnightShadowbringer.Dispose()',
    'darkKnightShadowbringer.Arm(arguments, nearAssist.Diagnostics.HookAvailable)',
    'shadowbringer[cmd={darkKnightShadowbringerCommandRegistered}',
    '{darkKnightShadowbringer.Diagnostics.ToChatLine()}'
) 'DRK /seitonbringer exact command, hook dependency, collision handling, lifecycle, and diagnostics'
if ([regex]::Matches($pluginSource, '\bnew\s+DarkKnightShadowbringerMacroService\s*\(').Count -ne 1 -or
    [regex]::Matches($pluginSource, '\bcommandManager\.AddHandler\(\s*DarkKnightShadowbringerMacroService\.Command').Count -ne 1 -or
    [regex]::Matches($pluginSource, '\bdarkKnightShadowbringer\.Arm\s*\(').Count -ne 1 -or
    $pluginSource -match '(?m)^\s*(?:private|internal|public).*?(?:SeitonbringerAlias|ShadowbringerAlias)') {
    throw 'DRK must own exactly one macro-allowed /seitonbringer command, one arm call, no alias, and no independent hook.'
}

$panicShukuchiRules = Read-RequiredSource $panicShukuchiRulesPath 'Panic Shukuchi rules'
$panicShukuchiService = Read-RequiredSource $panicShukuchiServicePath 'Panic Shukuchi service'
$panicShukuchiSelfTests = Read-RequiredSource $panicShukuchiSelfTestsPath 'Panic Shukuchi self-tests'
$panicShukuchiProgram = Read-RequiredSource (
    Join-Path $coreSelfTestRoot 'Program.cs') 'Panic Shukuchi test registry'
$panicShukuchiMetadata = Read-RequiredSource (
    Join-Path $pluginServicesRoot 'SeitonMetadataGuard.cs') 'Panic Shukuchi metadata guard'
$panicShukuchiConstants = Read-RequiredSource (
    Join-Path $pluginServicesRoot 'EnemyCombatConstants.cs') 'Panic Shukuchi constants'
$panicShukuchiConfiguration = Read-RequiredSource (
    Join-Path $sourceRoot 'SeitonSense.Plugin\Models\PluginConfiguration.cs') 'Plugin configuration'
$normalizedPanicShukuchiRules = $panicShukuchiRules -replace '\s+', ' '
$normalizedPanicShukuchiService = $panicShukuchiService -replace '\s+', ' '
$normalizedPanicShukuchiPlugin = $pluginSource -replace '\s+', ' '

Assert-Literals $panicShukuchiRules @(
    'public const uint NinjaJobId = 30',
    'public const uint ActionId = 29_513',
    'public const float NativeMaximumRangeYalms = 20f',
    'public const float SafeForwardDistanceYalms = 19.5f',
    'public const float MaximumGroundHorizontalErrorYalms = 0.05f',
    'context == SupportedPvPContext.CrystallineConflict',
    'wolvesDenTestingEnabled && context == SupportedPvPContext.WolvesDen',
    'MathF.Sin(rotationRadians)',
    'MathF.Cos(rotationRadians)',
    'IsApproximatelySafeHorizontalDistance(origin, probe)',
    'candidate.GroundHit.ExactGroundHit',
    'public readonly record struct PanicShukuchiCommandObservation(',
    'public static PanicShukuchiCommandDecision Evaluate(',
    'PanicShukuchiDecisionReason.Ready',
    'new PanicShukuchiIntent(',
    'observation.Candidate.GroundHit.Position'
) 'Pure immediate Panic Shukuchi policy'
if ($panicShukuchiRules -match '\b(?:UseAction|UseActionLocation)\s*\(|\b(?:ActionManager|IPlayerCharacter|BGCollisionModule|ITargetManager|TargetManager|SetTarget|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b|\.(?:Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)' -or
    $panicShukuchiRules -match '\b(?:PanicShukuchiPending|PanicShukuchiArm|ObservePending|MaximumPendingMilliseconds)\b' -or
    [regex]::Matches($panicShukuchiRules, '\bEvaluate\s*\(').Count -ne 1 -or
    [regex]::Matches($panicShukuchiRules, '\bTryCreateForwardProbe\s*\(').Count -lt 2) {
    throw 'Panic Shukuchi Core policy must remain pure, synchronous, stateless, and free of pending/native action/target/thread APIs.'
}
if ($normalizedPanicShukuchiRules -notmatch 'public readonly record struct PanicShukuchiCommandObservation\( bool PluginEnabled, bool MetadataVerified, SupportedPvPContext Context, bool WolvesDenTestingEnabled, uint LocalJobId, bool LocalPlayerAliveAndTargetable, uint ResolvedActionId, PanicShukuchiCandidate Candidate\);' -or
    $normalizedPanicShukuchiRules -notmatch 'public static PanicShukuchiCommandDecision Evaluate\( PanicShukuchiCommandObservation observation\).*?if \(!observation\.PluginEnabled\).*?if \(!observation\.MetadataVerified\).*?if \(!IsSupportedContext\(observation\.Context, observation\.WolvesDenTestingEnabled\)\).*?if \(!observation\.LocalPlayerAliveAndTargetable\).*?if \(observation\.LocalJobId != NinjaJobId\).*?if \(observation\.ResolvedActionId != ActionId\).*?if \(!IsValidGroundHit\(observation\.Candidate\)\).*?new PanicShukuchiIntent\( ActionId, observation\.Candidate\.GroundHit\.Position\)') {
    throw 'Panic Shukuchi must expose only synchronous static gates and one exact immediate intent; Guard/scheduler state cannot enter the policy.'
}

Assert-Literals $panicShukuchiService @(
    'internal const string Command = "/panicshu"',
    'Executes only an explicit /panicshu command',
    'immediately makes at most one native',
    'deliberately has no scheduler',
    'private const ulong DefaultTargetSentinel = 0xE0000000UL',
    'private const float GroundProbeStartAboveYalms = 5f',
    'private const float GroundProbeMaximumDistanceYalms = 10f',
    'metadata.PanicShukuchiVerified',
    'internal unsafe void Execute(string arguments)',
    'PanicShukuchiRules.Evaluate(',
    'actionManager->GetAdjustedActionId(',
    'adjustedActionId',
    'BGCollisionModule.RaycastMaterialFilter(',
    'new PanicShukuchiGroundHit(',
    'if (!decision.ShouldAttempt || decision.Intent is not { } intent)',
    'lock (diagnosticsGate) attemptCount++',
    'using var explicitGuardBreak = nearAssist.EnterExplicitAutoGuardBreak();',
    'actionManager->UseActionLocation(',
    'ActionType.Action',
    'PanicShukuchiRules.ActionId',
    'DefaultTargetSentinel',
    'Immediate native Shukuchi accepted',
    'Immediate native Shukuchi rejected',
    'mode=immediate'
) 'Explicit manual Panic Shukuchi runtime boundary'
if ([regex]::Matches($panicShukuchiService, '\bUseActionLocation\s*\(').Count -ne 1 -or
    [regex]::Matches($panicShukuchiService, '(?<!Location)\bUseAction\s*\(').Count -ne 0 -or
    [regex]::Matches($panicShukuchiService, '\bRaycastMaterialFilter\s*\(').Count -ne 1 -or
    [regex]::Matches($panicShukuchiService, '\bunsafe\b').Count -ne 1 -or
    $panicShukuchiService -match '(?-i:\b(?:IFramework|IKeyState|VirtualKey|TargetPressureTracker|Hook<|HookFromAddress|IGameInteropProvider|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event|RetryAction|RetryDispatch|BufferedDispatch|PendingDispatch|QueueAction|ExecuteAction|SendAction|OnFrameworkUpdate|IsPurifyPriorityClaimed|IsLocalGuardActiveOrPropagatingForPanicShukuchi|PanicShukuchiPending|MaximumPendingMilliseconds|ClientActionAttemptBoundary|ClientActionAttemptBoundaryRules|LastUsedActionSequence|IsActionOffCooldown|CheckActionResources|GetActionStatus)\b)|\.(?:StatusList|CastActionId|ActionQueued|AnimationLockSeconds|IsActionOffCooldown|ResourceStatus)\b|\.(?:Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)|->(?:ActionQueued|QueuedActionId|QueuedTargetId|AnimationLock|CastActionId)\s*=(?!=|>)' -or
    $panicShukuchiService -match '\b(?:Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b' -or
    $panicShukuchiService -match '\b(?:IChatGui|chatGui|PrintError)\b' -or
    $panicShukuchiService -match '\b(?:for|foreach|while|do)\s*\(') {
    throw 'Panic Shukuchi must remain one chat-silent immediate location call with no scheduler/Guard/CC/readiness gate, hook, target/cursor mutation, loop, retry, or fallback search.'
}
$panicDecisionIndex = $panicShukuchiService.IndexOf(
    'var decision = PanicShukuchiRules.Evaluate(',
    [System.StringComparison]::Ordinal)
$panicAttemptCountIndex = $panicShukuchiService.IndexOf(
    'lock (diagnosticsGate) attemptCount++;',
    [System.StringComparison]::Ordinal)
$panicNativeCallIndex = $panicShukuchiService.IndexOf(
    'actionManager->UseActionLocation(',
    [System.StringComparison]::Ordinal)
if ($panicDecisionIndex -lt 0 -or $panicAttemptCountIndex -lt 0 -or
    $panicNativeCallIndex -lt 0 -or $panicDecisionIndex -ge $panicAttemptCountIndex -or
    $panicAttemptCountIndex -ge $panicNativeCallIndex -or
    $normalizedPanicShukuchiService -notmatch 'internal unsafe void Execute\(string arguments\).*?var adjustedActionId = actionManager->GetAdjustedActionId\( PanicShukuchiRules\.ActionId\);.*?var decision = PanicShukuchiRules\.Evaluate\(.*?lock \(diagnosticsGate\) attemptCount\+\+; bool accepted; try \{ using var explicitGuardBreak = nearAssist\.EnterExplicitAutoGuardBreak\(\); accepted = actionManager->UseActionLocation\(') {
    throw 'Panic Shukuchi must validate and account for the explicit command immediately before its sole native location call in Execute.'
}

Assert-Literals $panicShukuchiConstants @(
    'PanicShukuchiActionId = 29513',
    'PanicShukuchiActionIconId = 9185',
    'PanicShukuchiRecast100ms = 200',
    'PanicShukuchiSheetRange = 20'
) 'Current-patch Panic Shukuchi constants'
Assert-Literals $panicShukuchiMetadata @(
    'bool PanicShukuchiVerified',
    'ValidateFeature("Panic Shukuchi"',
    'action.ClassJob.RowId == EnemyCombatConstants.NinjaJobId',
    'action.Range == EnemyCombatConstants.PanicShukuchiSheetRange',
    'action.EffectRange == 1',
    'action.CastType == 7',
    'action.Cast100ms == 0',
    'action.TargetArea',
    'action.RequiresLineOfSight',
    'action.NeedToFaceTarget',
    'action.AffectsPosition',
    'Action changes to Doton while under the effect of Three Mudra.'
) 'Current-patch Panic Shukuchi metadata gate'

Assert-Literals $pluginSource @(
    'private readonly PanicShukuchiService panicShukuchi',
    'panicShukuchi = new PanicShukuchiService(',
    'new CommandInfo(OnPanicShukuchiCommand)',
    'panicShukuchi.Execute(arguments)',
    'commandManager.RemoveHandler(PanicShukuchiService.Command)',
    'panic-shukuchi[cmd={panicShukuchiCommandRegistered}',
    '/panicshu immediately makes one NIN-only Shukuchi attempt 19.5 yalms straight ahead'
) 'Command-only immediate Panic Shukuchi ownership'
if ($normalizedPanicShukuchiPlugin -notmatch 'panicShukuchi = new PanicShukuchiService\( configuration, clientState, objectTable, dutyState, nearAssist, log, metadata\);.*?panicShukuchiCommandRegistered = commandManager\.AddHandler\( PanicShukuchiService\.Command, new CommandInfo\(OnPanicShukuchiCommand\).*?AllowedInMacros = true' -or
    [regex]::Matches($pluginSource, '\bnew\s+PanicShukuchiService\s*\(').Count -ne 1 -or
    [regex]::Matches($pluginSource, '\bcommandManager\.AddHandler\(\s*PanicShukuchiService\.Command').Count -ne 1 -or
    [regex]::Matches($pluginSource, '\bpanicShukuchi\.Execute\s*\(').Count -ne 1 -or
    $pluginSource -match '\bpanicShukuchi\.(?:Arm|Start|Dispose)\s*\(' -or
    [regex]::Matches($pluginSource, '\bOnPanicShukuchiCommand\s*\(').Count -ne 1) {
    throw 'Only one macro-allowed /panicshu handler may synchronously execute Panic, with no scheduler lifecycle or automatic source.'
}
$panicServiceTypeReferences = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bPanicShukuchiService\b')
if (@($panicServiceTypeReferences | Where-Object {
        $_.Path -notin @($pluginPath, $panicShukuchiServicePath)
    }).Count -ne 0 -or
    [regex]::Matches(($sourceFiles | ForEach-Object {
        Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
    }) -join "`n", '\bpanicShukuchi\.Execute\s*\(').Count -ne 1 -or
    $panicShukuchiConfiguration -match '\bPanicShukuchi\b') {
    throw 'Panic Shukuchi must remain command-only, with no configuration/automatic trigger and exactly one Plugin.cs execute provenance.'
}

$panicTestMethods = @(
    'ConstantsAndForwardAxesAreExact',
    'SupportedContextsAreExact',
    'GroundHitMustBeExactForwardFiniteAndInRange',
    'ValidCommandProducesOneImmediateIntent',
    'RepeatedCommandsAreIndependent',
    'CommandPolicyHasNoGuardOrSchedulerInputs',
    'StaticCommandGatesFailClosed',
    'InvalidActionOrTerrainExposesNoFallback'
)
foreach ($method in $panicTestMethods) {
    Assert-Literals $panicShukuchiSelfTests @("public static void $method()") "Panic Shukuchi test $method"
    Assert-Literals $panicShukuchiProgram @("PanicShukuchiSelfTests.$method") "Panic Shukuchi test registration $method"
}
if ([regex]::Matches($panicShukuchiSelfTests, '(?m)^\s*public static void \w+\(\)').Count -ne 8 -or
    [regex]::Matches($panicShukuchiProgram, '\bPanicShukuchiSelfTests\.\w+').Count -ne 8 -or
    $panicShukuchiSelfTests -match '\b(?:UseAction|UseActionLocation)\s*\(|\b(?:ActionManager|IPlayerCharacter|BGCollisionModule|ITargetManager|TargetManager)\b') {
    throw 'All eight pure immediate Panic Shukuchi tests and their exact Core registry entries must remain pinned.'
}

# The automatic NIN Guard-Shukuchi helper is deliberately separate from the
# immediate /panicshu command. It owns one exact held actor intent, one reviewed
# location boundary, and one post-acceptance exact hard-target setter.
$ninjaGuardShukuchiRules = Read-RequiredSource $ninjaGuardShukuchiRulesPath 'NIN Guard-Shukuchi rules'
$ninjaGuardShukuchiProbe = Read-RequiredSource $ninjaGuardShukuchiProbePath 'NIN Guard-Shukuchi probe'
$ninjaGuardShukuchiSelfTests = Read-RequiredSource $ninjaGuardShukuchiSelfTestsPath 'NIN Guard-Shukuchi self-tests'
$normalizedNinjaGuardShukuchiRules = $ninjaGuardShukuchiRules -replace '\s+', ' '
$normalizedNinjaGuardShukuchiProbe = $ninjaGuardShukuchiProbe -replace '\s+', ' '
Assert-Literals $ninjaGuardShukuchiRules @(
    'public const uint NinjaJobId = 30',
    'public const uint ActionId = 29_513',
    'public const uint GuardStatusId = 3_054',
    'public const uint GuardStatusAlternateId = 3_673',
    'public const float NativeMaximumRangeYalms = 20f',
    '(ulong)currentHp * 100UL < (ulong)maximumHp * 20UL',
    'candidate.GuardActive',
    'candidate.WithinNativeRange',
    'candidate.Position.IsFinite',
    'leftPositivePressure',
    'HasUnambiguousCandidateIdentities',
    'NinjaGuardShukuchiHoldState BeginAcceptedHold',
    'ObservedCooldownUnavailable = true',
    'TrySpendReadyEpoch('
) 'Pure exact NIN Guard-Shukuchi policy'
if ($ninjaGuardShukuchiRules -match '\b(?:UseAction|UseActionLocation)\s*\(|\b(?:ActionManager|IPlayerCharacter|ITargetManager|TargetManager|SetTarget|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b|\.(?:Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)' -or
    $normalizedNinjaGuardShukuchiRules -notmatch 'public static bool IsStrictlyBelowTwentyPercent\(uint currentHp, uint maximumHp\).*?\(ulong\)currentHp \* 100UL < \(ulong\)maximumHp \* 20UL;' -or
    $normalizedNinjaGuardShukuchiRules -notmatch 'public static bool CanUseExactIntent\(.*?currentCandidate\.EnemySlot == intent\.EnemySlot && currentCandidate\.Actor == intent\.Target && IsEligibleCandidate\(currentCandidate, currentLocal\);') {
    throw 'Guard-Shukuchi Core must stay pure, use strict overflow-safe <20%, and revalidate only the frozen exact actor.'
}

Assert-Literals $ninjaGuardShukuchiProbe @(
    'private const ulong DefaultTargetSentinel = 0xE0000000UL',
    'MaximumPressureAgeMilliseconds = 250',
    'EnemySlotResolver.Resolve(objectTable, slot)',
    'NinjaGuardShukuchiRules.CanUseExactIntent(',
    'actionManager->GetAdjustedActionId(',
    'actionManager->IsActionOffCooldown(',
    'actionManager->CheckActionResources(',
    'actionManager->UseActionLocation(',
    'ActionType.Action',
    'DefaultTargetSentinel',
    'ClientActionAttemptBoundaryRules.Classify(',
    'outcome == ClientActionAttemptOutcome.ClientAccepted',
    'TrySetExactHardTargetOnce(intent)',
    'targetManager.Target = target;',
    'MatchesExactTarget(targetManager.Target, target)',
    'HeldCastCancellationHelperKind.NinjaGuardShukuchi',
    'HeldActionRetryRules.Complete(',
    'NinjaGuardShukuchiRules.ObserveAcceptedHold(',
    'NinjaGuardShukuchiRules.TrySpendReadyEpoch(',
    'IsOwnGuardActiveOrPropagating(localPlayer)',
    'DefensiveUtilityProbe.HasActiveGuard(localPlayer)',
    'nearAssist.TryGetRecentExactLocalGuardAttempt(',
    'DefensiveUtilityRules.GuardPropagationLatchMilliseconds',
    'float.IsFinite(status.RemainingTime)',
    'status.RemainingTime > 0f'
) 'Reviewed held NIN Guard-Shukuchi native and target boundary'
if ([regex]::Matches($ninjaGuardShukuchiProbe, '\bUseActionLocation\s*\(').Count -ne 1 -or
    [regex]::Matches($ninjaGuardShukuchiProbe, '(?<!Location)\bUseAction\s*\(').Count -ne 0 -or
    [regex]::Matches($ninjaGuardShukuchiProbe, '(?m)^\s*targetManager\.Target\s*=\s*target;\s*$').Count -ne 1 -or
    $normalizedNinjaGuardShukuchiProbe -notmatch 'NinjaGuardShukuchiRules\.ObserveAcceptedHold\( acceptedHold, hardReset,.*?readinessKnown && resolvedActionId == NinjaGuardShukuchiRules\.ActionId, cooldownReady\);' -or
    $normalizedNinjaGuardShukuchiProbe -notmatch 'var destination = new Vector3\(.*?if \(IsOwnGuardActiveOrPropagating\(localPlayer\)\) return false; before = ClientActionAttemptBoundary\.Capture\(actionManager, intent\.ActionId\); attemptedAtBoundary = true; var accepted = actionManager->UseActionLocation\(' -or
    $normalizedNinjaGuardShukuchiProbe -notmatch 'var outcome = attemptedAtBoundary.*?if \(outcome == ClientActionAttemptOutcome\.ClientAccepted\) hardTargetConfirmed = TrySetExactHardTargetOnce\(intent\); return outcome;' -or
    $normalizedNinjaGuardShukuchiProbe -notmatch 'private bool TrySetExactHardTargetOnce\(NinjaGuardShukuchiIntent intent\).*?EnemySlotResolver\.Resolve\(objectTable, intent\.EnemySlot\).*?target!?\.GameObjectId != intent\.Target\.GameObjectId.*?target\.EntityId != intent\.Target\.EntityId.*?objectTable\.SearchByEntityId\(target\.EntityId\).*?tableTarget!?\.Address != target\.Address.*?targetManager\.Target = target; return MatchesExactTarget\(targetManager\.Target, target\);' -or
    $ninjaGuardShukuchiProbe -match '\b(?:BGCollisionModule|RaycastMaterialFilter|ResolveWolvesDenDuelOpponent|RetryAction|RetryDispatch|FallbackTarget|AlternateTarget|MouseOverTarget|FocusTarget|SoftTarget)\b') {
    throw 'Held Guard-Shukuchi must own exactly one same-actor location call and one exact post-client-acceptance hard-target write, with no terrain reroute, alternate, fallback, or other target mutation.'
}

$ninjaGuardShukuchiTestMethods = @(
    'ConstantsAndStrictThresholdAreExact',
    'NativeRangeAndPositionAreExact',
    'CandidateRequiresEveryGuardLowHpGate',
    'PositivePressureIsOnlyARankingBonus',
    'PartialSlotsWorkButAmbiguityFailsClosed',
    'DispatchRequiresEveryStaticAndInputGate',
    'FrozenIntentCannotRerankOrDrift',
    'CastCancellationAndRetryKeepExactIntent',
    'ContinuousHoldRequiresProvenCooldownRearm'
)
foreach ($method in $ninjaGuardShukuchiTestMethods) {
    Assert-Literals $ninjaGuardShukuchiSelfTests @("public static void $method()") "NIN Guard-Shukuchi test $method"
    Assert-Literals $panicShukuchiProgram @("NinjaGuardShukuchiSelfTests.$method") "NIN Guard-Shukuchi registration $method"
}
if ([regex]::Matches($ninjaGuardShukuchiSelfTests, '(?m)^\s*public static void \w+\(\)').Count -ne 9 -or
    [regex]::Matches($panicShukuchiProgram, '\bNinjaGuardShukuchiSelfTests\.\w+').Count -ne 9 -or
    $ninjaGuardShukuchiSelfTests -match '\b(?:UseAction|UseActionLocation)\s*\(|\b(?:ActionManager|IPlayerCharacter|ITargetManager|TargetManager)\b') {
    throw 'All nine pure Guard-Shukuchi tests and their exact Core registry entries must remain pinned.'
}

# Action initiation remains globally forbidden except for one exact self-Purify,
# one exact job-gated ally-rescue, the exact defensive Guard/Guardian boundary,
# one exact WHM/BRD/NIN reactive-CC boundary, one exact default-off NIN Seiton
# boundary, one exact default-off SCH Critical Strategy boundary, one exact
# Eukrasia-triggered SGE Smart Kardia boundary, one exact self-only Smart
# Recuperate boundary, one exact default-off Monk Earth's Reply call, and one
# exact job-tier default-off DRK Plunge call, and the explicit command-only NIN
# Panic Shukuchi location call. Near Assist/Near Help/Far Help may
# forward an incoming action through their sole Original. The same reviewed
# detour may issue exactly one spent DRK Shadowbringer call before leaving the
# original Souleater carrier unchanged; that boundary is pinned below.
$actionMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(UseAction|UseActionLocation|ExecuteAction|SendAction)\s*\(')
$unexpectedAction = @($actionMatches | Where-Object {
    $reviewedActionBoundary =
        $_.Path -in @($purifyProbePath, $defensiveUtilityProbePath, $pressureEscapeSprintProbePath, $allyRescueProbePath, $miracleInterceptProbePath, $ninjaSeitonProbePath, $scholarCriticalStrategyProbePath, $smartKardiaProbePath, $smartRecuperateProbePath, $monkEarthReplyProbePath, $darkKnightPlungeProbePath, $nearAssistPath) -and
        $_.Line -match '\bUseAction\b'
    $reviewedPanicLocationBoundary =
        $_.Path -in @($panicShukuchiServicePath, $ninjaGuardShukuchiProbePath) -and
        $_.Line -match '\bUseActionLocation\b'
    $reviewedBrakeDocumentation =
        $_.Path -eq $ccImmunityBrakeTargetRulesPath -and
        $_.Line -match '^\s*///.*\bUseAction\b'
    $reviewedAttemptDocumentation =
        $_.Path -eq $clientActionAttemptOutcomePath -and
        $_.Line -match '^\s*///.*\bUseAction\b'
    -not ($reviewedActionBoundary -or $reviewedPanicLocationBoundary -or $reviewedBrakeDocumentation -or $reviewedAttemptDocumentation)
})
if ($unexpectedAction.Count -gt 0) {
    $locations = $unexpectedAction | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only the reviewed action probes, bounded shared macro detour, explicit PanicShukuchiService, and held NIN Guard-Shukuchi location boundaries may initiate actions: $($locations -join ', ')"
}

# All party-visible commands share one closed, typed dispatcher. It remains the
# sole raw RaptureShell write boundary. The DRK macro service may only read the
# native macro cursor/name/text needed to prove the exact adjacent macro pair.
$rawShellApiMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(RaptureShellModule|GetRaptureShellModule|ExecuteCommandInner|Utf8String\.FromString)\b')
$unexpectedRawShellApis = @($rawShellApiMatches | Where-Object {
    $reviewedDispatcherBoundary = $_.Path -eq $reviewedPvpCommandDispatcherPath
    $reviewedDrkReadOnlyBoundary =
        $_.Path -eq $darkKnightShadowbringerServicePath -and
        $_.Line -match '\bRaptureShellModule\.Instance\s*\('
    -not ($reviewedDispatcherBoundary -or $reviewedDrkReadOnlyBoundary)
})
if ($unexpectedRawShellApis.Count -gt 0) {
    $locations = $unexpectedRawShellApis | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only ReviewedPvpCommandDispatcher may write through RaptureShell; DRK may only read its exact macro cursor: $($locations -join ', ')"
}
$markerTelemetryMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bMarkingController\b')
$unexpectedMarkerTelemetry = @($markerTelemetryMatches | Where-Object {
    $_.Path -notin @($autoEnemyFocusMarkPath, $guardianCommunicationPath)
})
if ($unexpectedMarkerTelemetry.Count -gt 0) {
    $locations = $unexpectedMarkerTelemetry | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only Team Attack-1 and Guardian communication may read native marker telemetry: $($locations -join ', ')"
}

$reviewedPvpCommandDispatcher = Read-RequiredSource $reviewedPvpCommandDispatcherPath 'Reviewed PvP command dispatcher'
$normalizedReviewedPvpCommandDispatcher = $reviewedPvpCommandDispatcher -replace '\s+', ' '
Assert-Literals $reviewedPvpCommandDispatcher @(
    'internal const long MinimumMarkerCommandIntervalMilliseconds = 100',
    'private readonly object markerReservationGate = new()',
    'ReviewedPvpCommandDispatchResult.MarkerRateLimited',
    'lastMarkerReservationAt = nowMilliseconds',
    'private static string? ResolveExactHardcodedCommand(ReviewedPvpCommand command)',
    'private static unsafe bool TryExecuteShellCommand(string exactHardcodedCommand)',
    'UIModule.Instance()',
    'uiModule->GetRaptureShellModule()',
    'Utf8String.FromString(exactHardcodedCommand)',
    'shell->ExecuteCommandInner(command, uiModule)',
    'command->Dtor(true)'
) 'Single closed reviewed PvP shell-command boundary'
if ([regex]::Matches($reviewedPvpCommandDispatcher, '\bExecuteCommandInner\s*\(').Count -ne 1 -or
    [regex]::Matches($reviewedPvpCommandDispatcher, '\bUtf8String\.FromString\s*\(').Count -ne 1 -or
    [regex]::Matches($reviewedPvpCommandDispatcher, '\bTryExecuteShellCommand\s*\(').Count -ne 3 -or
    $reviewedPvpCommandDispatcher -match '(?m)^\s*(?:public|internal)\s+[^\r\n(]+\([^\r\n)]*\bstring\b' -or
    $normalizedReviewedPvpCommandDispatcher -notmatch 'lock \(markerReservationGate\).*?if \(!MarkerIntervalElapsed\(nowMilliseconds\)\).*?return ReviewedPvpCommandDispatchResult\.MarkerRateLimited;.*?lastMarkerReservationAt = nowMilliseconds;.*?return TryExecuteShellCommand\(exactHardcodedCommand\)') {
    throw 'The shared dispatcher must expose no arbitrary-string API, reserve each marker request before one native shell invocation, and keep one UTF-8 command lifetime.'
}

$expectedReviewedCommands = @()
$japaneseCoveringTarget = -join @(
    [char]0x63F4,
    [char]0x8B77,
    [char]0xFF1A,
    [char]0x30BF,
    [char]0x30FC,
    [char]0x30B2,
    [char]0x30C3,
    [char]0x30C8)
foreach ($slot in 1..5) {
    $expectedReviewedCommands += "/mk attack1 <e$slot>"
    $expectedReviewedCommands += "/mk off <e$slot>"
}
foreach ($slot in 1..8) {
    $expectedReviewedCommands += ('/quickchat "Covering Target" <{0}>' -f $slot)
    $expectedReviewedCommands += ('/schnellchat <{0}> Ziel decken' -f $slot)
    $expectedReviewedCommands += ('/quickchat "Soutien : cible" <{0}>' -f $slot)
    $expectedReviewedCommands += ('/quickchat "{0}" <{1}>' -f $japaneseCoveringTarget, $slot)
    $expectedReviewedCommands += "/mk bind2 <$slot>"
}
$expectedReviewedCommands += @(
    '/mk bind1 <me>',
    '/mk off <bind1>',
    '/mk off <bind2>'
)
$reviewedCommandLiterals = @([regex]::Matches(
        $reviewedPvpCommandDispatcher,
        '(?m)=>\s*"(?<Command>/(?:mk|quickchat|schnellchat).*)",\s*$') |
    ForEach-Object { $_.Groups['Command'].Value -replace '\\"', '"' })
$actualReviewedCommandSet = ($reviewedCommandLiterals | Sort-Object) -join '|'
$expectedReviewedCommandSet = ($expectedReviewedCommands | Sort-Object) -join '|'
$allReviewedCommandLiteralLines = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '=>\s*"/(?:mk|quickchat|schnellchat)')
if ($reviewedCommandLiterals.Count -ne 53 -or
    $actualReviewedCommandSet -ne $expectedReviewedCommandSet -or
    $allReviewedCommandLiteralLines.Count -ne 53 -or
    @($allReviewedCommandLiteralLines | Where-Object { $_.Path -ne $reviewedPvpCommandDispatcherPath }).Count -gt 0 -or
    $reviewedPvpCommandDispatcher -match '"/p\s|<t>|<e[1-5]>.*Guardian|Guardian.*<e[1-5]>') {
    throw 'The dispatcher allowlist must contain exactly 53 reviewed commands: Attack1 set/clear e1-e5, localized row-35 Quick Chat P1-P8, Bind2 P1-P8, Bind1 self, and exact Bind clears.'
}

$autoEnemyFocusMark = Read-RequiredSource $autoEnemyFocusMarkPath 'Auto enemy focus mark service'
Assert-Literals $autoEnemyFocusMark @(
    'ReviewedPvpCommandDispatcher commands',
    'commands.TryMarkAttack1(desired.Value.EnemySlot, now)',
    'commands.TryClearAttack1(owned.EnemySlot, now)',
    'ReviewedPvpCommandDispatchResult.MarkerRateLimited',
    'MarkingController.Instance()'
) 'Team Attack-1 typed dispatcher and telemetry ownership'
if ([regex]::Matches($autoEnemyFocusMark, '\bMarkingController\.Instance\s*\(').Count -lt 2 -or
    $autoEnemyFocusMark -match '\b(RaptureShellModule|GetRaptureShellModule|ExecuteCommandInner|Utf8String\.FromString|TryExecuteShellCommand)\b' -or
    $autoEnemyFocusMark -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=|Markers\s*\[[^\]]+\]\s*=|MarkerTimes\s*\[[^\]]+\]\s*=') {
    throw 'Team Attack-1 must retain read-only marker telemetry and typed shared-dispatcher calls without target mutation, raw marker writes, or its own shell boundary.'
}

# Guardian team communication starts only from the strong event emitted by the
# sole client-accepted automatic Guardian request. Core owns one-shot episode
# consumption, marker ownership, confirmation, sequencing, and cleanup.
$guardianTeamCommunicationRules = Read-RequiredSource $guardianTeamCommunicationRulesPath 'Guardian team communication rules'
$normalizedGuardianTeamCommunicationRules = $guardianTeamCommunicationRules -replace '\s+', ' '
$guardianTeamCommunicationSelfTests = Read-RequiredSource $guardianTeamCommunicationSelfTestsPath 'Guardian team communication self-tests'
$coreSelfTestProgramForGuardian = Read-RequiredSource (
    Join-Path $coreSelfTestRoot 'Program.cs') 'Core self-test registry'
Assert-Literals $guardianTeamCommunicationRules @(
    'GuardianTeamCommunicationState Initial',
    'GuardianTeamCommunicationDecision(',
    'public bool ShouldIssueCommand =>',
    'public const int Bind1MarkerIndex = 5;',
    'public const int Bind2MarkerIndex = 6;',
    'public const ulong InvalidMarkerGameObjectId = 0xE0000000UL;',
    'public static bool IsEmptyMarkerGameObjectId(ulong gameObjectId) =>',
    'gameObjectId is 0 or InvalidMarkerGameObjectId;',
    'public const long ActiveLifetimeMilliseconds = 9_000;',
    'public const long CommandConfirmationTimeoutMilliseconds = 1_500;',
    'var consumed = ToIdle(episode.Token);',
    'state = state with { LastConsumedEpisodeToken = observedEpisode.Token };',
    'var pairPlanned = BothMarkersExactlyEmpty(observation);',
    'SaturatingAdd(episode.AcceptedAtMilliseconds, ActiveLifetimeMilliseconds)',
    'GuardianTeamCommunicationCommandKind.SendQuickChat',
    'GuardianTeamCommunicationCommandKind.SetBind2',
    'GuardianTeamCommunicationCommandKind.SetBind1',
    'GuardianTeamCommunicationCommandKind.ClearBind2',
    'GuardianTeamCommunicationCommandKind.ClearBind1',
    'marker.GameObjectId == expectedActor.GameObjectId',
    'marker.MarkerTime != markerTimeBeforeCommand',
    'observation.Bind2.MarkerTime != bind2.MarkerTime',
    'observation.Bind1.MarkerTime != bind1.MarkerTime',
    'ClearBind2Command(state.Episode!.Value, owned.MarkerTime)',
    'ClearBind1Command(state.Episode!.Value, bind1Owned.MarkerTime)',
    'ExpectedMarkerTime'
) 'Pure accepted-Guardian communication FSM'
$pendingBind2ConfirmationIndex = $normalizedGuardianTeamCommunicationRules.IndexOf(
    'if (state.Phase == GuardianTeamCommunicationPhase.AwaitingBind2Confirmation)')
$pendingBind1ConfirmationIndex = $normalizedGuardianTeamCommunicationRules.IndexOf(
    'if (state.Phase == GuardianTeamCommunicationPhase.AwaitingBind1Confirmation)')
$configurationGateIndex = $normalizedGuardianTeamCommunicationRules.IndexOf(
    'if (!observation.ConfigurationEnabled)')
$cleanupBind2Index = $normalizedGuardianTeamCommunicationRules.IndexOf(
    'if (state.OwnsBind2)',
    $normalizedGuardianTeamCommunicationRules.IndexOf('private static GuardianTeamCommunicationDecision ObserveCleanup'))
$cleanupBind1Index = $normalizedGuardianTeamCommunicationRules.IndexOf(
    'var bind1Owned = state.Bind1Ownership!.Value;',
    [Math]::Max(0, $cleanupBind2Index))
if ($pendingBind2ConfirmationIndex -lt 0 -or
    $pendingBind1ConfirmationIndex -le $pendingBind2ConfirmationIndex -or
    $configurationGateIndex -le $pendingBind1ConfirmationIndex -or
    $cleanupBind2Index -lt 0 -or
    $cleanupBind1Index -le $cleanupBind2Index -or
    $normalizedGuardianTeamCommunicationRules -notmatch 'if \(outcome == GuardianTeamCommunicationCommandOutcome\.DeferredBeforeInvocation && command\.Kind != GuardianTeamCommunicationCommandKind\.SendQuickChat\).*?Phase = ReadyPhaseFor\(command\.Kind\).*?PendingCommand = null' -or
    $normalizedGuardianTeamCommunicationRules -notmatch 'GuardianTeamCommunicationCommandKind\.SendQuickChat => AdvanceAfterQuickChat\(state\).*?GuardianTeamCommunicationCommandKind\.SetBind2 => outcome == GuardianTeamCommunicationCommandOutcome\.Invoked.*?AwaitingBind2Confirmation.*?: ToIdle\(state\.LastConsumedEpisodeToken\).*?GuardianTeamCommunicationCommandKind\.SetBind1 => outcome == GuardianTeamCommunicationCommandOutcome\.Invoked.*?AwaitingBind1Confirmation.*?: StartCleanupOrIdle' -or
    $normalizedGuardianTeamCommunicationRules -notmatch 'public bool HasExactShape\(int expectedIndex\) => Available && MarkerIndex == expectedIndex && MarkerTime >= 0; public bool IsExactlyEmpty\(int expectedIndex\) => HasExactShape\(expectedIndex\) && GuardianTeamCommunicationRules\.IsEmptyMarkerGameObjectId\(GameObjectId\);' -or
    $normalizedGuardianTeamCommunicationRules -notmatch 'public static bool IsEmptyMarkerGameObjectId\(ulong gameObjectId\) => gameObjectId is 0 or InvalidMarkerGameObjectId;' -or
    [regex]::Matches($guardianTeamCommunicationRules, '\bIsEmptyMarkerGameObjectId\s*\(').Count -ne 2 -or
    $normalizedGuardianTeamCommunicationRules -notmatch 'observation\.Bind1\.IsExactlyEmpty\(Bind1MarkerIndex\) && observation\.Bind2\.IsExactlyEmpty\(Bind2MarkerIndex\).*?observation\.Bind1\.MarkerTime == state\.Bind1MarkerTimeBeforeSet && observation\.Bind2\.MarkerTime == state\.Bind2MarkerTimeBeforeSet' -or
    $normalizedGuardianTeamCommunicationRules -notmatch 'if \(!observation\.TextInputStateKnown \|\| observation\.TextInputActive\).*?StartCleanupOrIdle\(state\).*?GuardianTeamCommunicationDecisionKind\.Waiting' -or
    $guardianTeamCommunicationRules -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|HookFromAddress|ITargetManager|TargetManager|SetTarget|RaptureShellModule|ExecuteCommandInner|MarkingController)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Guardian Core must consume accepted episodes before gates, offer Quick Chat once, allow only pre-invocation marker deferral, confirm async marker ownership through config/text loss, and clear Bind2 before Bind1 without gameplay or shell access.'
}

$guardianSelfTestMethods = @(
    'AcceptedEpisodeQuickChatIsOneShot',
    'InitialFailuresConsumeWithoutCommands',
    'OccupiedOrUnknownMarkersStayQuickChatOnly',
    'InvalidNativeMarkerSentinelIsExactlyEmpty',
    'MarkerPairIsSequentialAndExactlyConfirmed',
    'SetConfirmationRequiresActorAndChangedTime',
    'PartialBind1FailureCleansOnlyOwnedBind2',
    'DeadlineCleanupIsBind2ThenBind1',
    'ExternalDriftCleansOnlyRemainingOwnership',
    'ResetAndContextLossOnlyUseSafeCleanup',
    'PendingConfirmationSurvivesConfigAndTextUntilCleanupIsSafe',
    'DeferredBeforeInvocationIsTheOnlyRepeatableDecision',
    'NewEpisodeWhileBusyIsConsumedWithoutReplacement'
)
foreach ($method in $guardianSelfTestMethods) {
    Assert-Literals $guardianTeamCommunicationSelfTests @("internal static void $method()") "Guardian communication self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("GuardianTeamCommunicationSelfTests.$method") "Guardian communication test registration $method"
}
Assert-Literals $guardianTeamCommunicationSelfTests @(
    'var emptySentinel = GuardianTeamCommunicationRules.InvalidMarkerGameObjectId;',
    'bind1GameObjectId: emptySentinel',
    'bind2GameObjectId: emptySentinel',
    'GuardianTeamCommunicationCommandKind.SendQuickChat',
    '"sentinel-empty Bind2 is set first"',
    'GuardianTeamCommunicationPhase.AwaitingBind2Confirmation',
    'GuardianTeamCommunicationPhase.ReadyToSetBind1',
    '"sentinel-empty Bind1 is set second"',
    'GuardianTeamCommunicationPhase.AwaitingBind1Confirmation',
    'GuardianTeamCommunicationPhase.ActivePair',
    'active.State.OwnsBind1 && active.State.OwnsBind2'
) 'Guardian native-invalid empty sentinel sequence and ownership test'
if ([regex]::Matches($guardianTeamCommunicationSelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 13 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bGuardianTeamCommunicationSelfTests\.\w+').Count -ne 13) {
    throw 'All thirteen Guardian communication lifecycle, native-empty-sentinel, ownership, cleanup, and fail-closed self-tests must remain registered exactly once.'
}

$defensiveUtilityGuardianSource = Read-RequiredSource $defensiveUtilityProbePath 'Defensive utility Guardian event source'
$normalizedDefensiveUtilityGuardianSource = $defensiveUtilityGuardianSource -replace '\s+', ' '
Assert-Literals $defensiveUtilityGuardianSource @(
    'internal readonly record struct AcceptedAutoGuardianEpisode(',
    'AcceptedAutoGuardianEpisode? LastAcceptedGuardianEpisode,',
    'private AcceptedAutoGuardianEpisode? lastAcceptedGuardianEpisode;',
    'private long guardianEpisodeToken;',
    'private unsafe ClientActionAttemptOutcome TryUseGuardianOnce(',
    'PaladinGuardianCandidate currentCandidate,',
    'accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;',
    'CompleteGuardianAttempt(frozen, outcome, nowMilliseconds);',
    'if (attempted && accepted)',
    'lastAcceptedGuardianEpisode = new AcceptedAutoGuardianEpisode(',
    'NextGuardianEpisodeToken()',
    'selected.Actor',
    'selected.PartySlot',
    'lastAcceptedGuardianEpisode = null;',
    'var current = Volatile.Read(ref guardianEpisodeToken);',
    'if (current == long.MaxValue) return long.MaxValue;',
    'var next = current + 1;',
    'Interlocked.CompareExchange(ref guardianEpisodeToken, next, current) == current'
) 'Client-accepted automatic Guardian episode source'
$acceptedGuardianConstructors = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bnew\s+AcceptedAutoGuardianEpisode\s*\(')
$resetRuntimeMethod = [regex]::Match(
    $normalizedDefensiveUtilityGuardianSource,
    'private void ResetRuntime\(\) \{(?<Body>.*?)\} private void ResetOpportunityRuntime').Groups['Body'].Value
if ($acceptedGuardianConstructors.Count -ne 2 -or
    @($acceptedGuardianConstructors | Where-Object { $_.Path -ne $defensiveUtilityProbePath }).Count -ne 0 -or
    $normalizedDefensiveUtilityGuardianSource -notmatch 'inputClaimed = true; inputFrame\.Consume\(\); var frozen = new FrozenGuardianRetry\(.*?var outcome = TryUseGuardianOnce\( localPlayer!, selected, selected, out attempted\); accepted = outcome == ClientActionAttemptOutcome\.ClientAccepted; CompleteGuardianAttempt\(frozen, outcome, nowMilliseconds\);.*?if \(attempted && accepted\) \{ lastAcceptedGuardianEpisode = new AcceptedAutoGuardianEpisode\( NextGuardianEpisodeToken\(\), Math\.Max\(nowMilliseconds, Environment\.TickCount64\), new TargetPressureActorIdentity\( localPlayer!\.GameObjectId, localPlayer\.EntityId\), selected\.Actor, selected\.PartySlot\); \}' -or
    $normalizedDefensiveUtilityGuardianSource -notmatch 'if \(frozenGuardianRetry is \{ \} frozenGuardian\).*?var exactCandidate = guardianCandidates\.FirstOrDefault\(candidate => candidate\.PartySlot == frozenGuardian\.Intent\.PartySlot && candidate\.Actor == frozenGuardian\.Intent\.Actor\);.*?var outcome = TryUseGuardianOnce\( localPlayer!, frozenGuardian\.Intent, exactCandidate, out attempted\); accepted = outcome == ClientActionAttemptOutcome\.ClientAccepted; CompleteGuardianAttempt\(frozenGuardian, outcome, nowMilliseconds\);.*?if \(accepted\) \{ lastAcceptedGuardianEpisode = new AcceptedAutoGuardianEpisode\( NextGuardianEpisodeToken\(\), Math\.Max\(nowMilliseconds, Environment\.TickCount64\), frozenGuardian\.LocalPlayer, frozenGuardian\.Intent\.Actor, frozenGuardian\.Intent\.PartySlot\); \}' -or
    $normalizedDefensiveUtilityGuardianSource -notmatch 'private long NextGuardianEpisodeToken\(\) \{ while \(true\) \{ var current = Volatile\.Read\(ref guardianEpisodeToken\); if \(current == long\.MaxValue\) return long\.MaxValue; var next = current \+ 1; if \(Interlocked\.CompareExchange\(ref guardianEpisodeToken, next, current\) == current\) return next; \} \}' -or
    [string]::IsNullOrWhiteSpace($resetRuntimeMethod) -or
    $resetRuntimeMethod -notmatch 'lastAcceptedGuardianEpisode = null;' -or
    $resetRuntimeMethod -match 'guardianEpisodeToken') {
    throw 'Only client-accepted initial or frozen-retry Guardian boundaries may publish a monotonically tokened confirmation event; reset clears the event but never rewinds the token.'
}

$guardianCommunicationMetadataGuard = Read-RequiredSource $guardianCommunicationMetadataGuardPath 'Guardian communication metadata guard'
$japaneseMetadataLiteral = 'ClientLanguage.Japanese => "' + $japaneseCoveringTarget + '",'
Assert-Literals $guardianCommunicationMetadataGuard @(
    'internal const uint QuickChatRowId = 35;',
    'internal const int QuickChatIconId = 9964;',
    'internal const uint QuickChatAddonRowId = 11718;',
    'internal const uint QuickChatTransientRowId = 52;',
    'ClientLanguage.English => "Covering Target"',
    'ClientLanguage.German => "Ziel decken"',
    'ClientLanguage.French => "Soutien : cible"',
    $japaneseMetadataLiteral,
    'dataManager.GetExcelSheet<QuickChat>(language)',
    'quickChat.RowId == QuickChatRowId',
    'quickChat.NameAction.ToString() == expectedName',
    'quickChat.Icon == QuickChatIconId',
    'quickChat.Addon.RowId == QuickChatAddonRowId',
    'quickChat.QuickChatTransient.RowId == QuickChatTransientRowId',
    'return new GuardianCommunicationMetadataValidation(false, language)'
) 'Exact localized Quick Chat row-35 metadata guard'
if ($guardianCommunicationMetadataGuard -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|HookFromAddress|ITargetManager|TargetManager|SetTarget|RaptureShellModule|ExecuteCommandInner|MarkingController)\b') {
    throw 'Guardian Quick Chat metadata validation must remain read-only and fail closed.'
}

$guardianCommunication = Read-RequiredSource $guardianCommunicationPath 'Guardian communication runtime'
$normalizedGuardianCommunication = $guardianCommunication -replace '\s+', ' '
Assert-Literals $guardianCommunication @(
    'AcceptedAutoGuardianEpisode? acceptedEpisode',
    'new GuardianTeamCommunicationEpisode(',
    'ResolveSupportedPvPContext() == SupportedPvPContext.CrystallineConflict',
    'GuardianTeamCommunicationRules.Observe(state, observation)',
    'if (decision.ShouldIssueCommand && decision.Command is { } command)',
    'GuardianTeamCommunicationRules.ApplyCommandResult(state, command, outcome)',
    'LastConsumedEpisodeToken = Math.Max(',
    'state.LastConsumedEpisodeToken,',
    'lastObservedEpisodeToken)',
    'configuration.Enabled &&',
    'configuration.PaladinGuardianLowAlly &&',
    'configuration.PaladinGuardianAnnounceAndMark &&',
    'metadata.Verified &&',
    'clientState.ClientLanguage == metadata.Language',
    'localPlayer.ClassJob.RowId == EnemyCombatConstants.PaladinJobId',
    'private static unsafe bool TryGetExactIdentity(',
    'private static unsafe void ReadMarkerObservations(',
    'private static unsafe bool TryGetTextInputState(',
    'PartySlotResolver.Resolve(objectTable, partySlot)',
    'native->EntityId != player.EntityId',
    'MarkingController.Instance()',
    'GuardianTeamCommunicationRules.Bind1MarkerIndex',
    'GuardianTeamCommunicationRules.Bind2MarkerIndex',
    'commands.TryQuickChatCoveringTarget(',
    'commands.TryMarkGuardianAlly(command.PartySlot, nowMilliseconds)',
    'commands.TryMarkGuardianSelf(nowMilliseconds)',
    'commands.TryClearGuardianAlly(nowMilliseconds)',
    'commands.TryClearGuardianSelf(nowMilliseconds)',
    'ReviewedPvpCommandDispatchResult.MarkerRateLimited',
    'GuardianTeamCommunicationCommandOutcome.DeferredBeforeInvocation',
    'GuardianTeamCommunicationCommandOutcome.TerminalFailure',
    'invoked=',
    'TryClearOneExactOwnershipOnDispose('
) 'Exact CC-only Guardian communication runtime mapping'
if ($guardianCommunication -match '\bconfiguration\.EnableDefensiveUtilities\b') {
    throw 'PLD Guardian communication must follow the independent Job Tools Guardian master, never the broad reactive-Guard master.'
}
$guardianObserveMethod = [regex]::Match(
    $normalizedGuardianCommunication,
    'internal GuardianCommunicationDiagnostics Observe\(.*?\) \{(?<Body>.*?)\} internal void Reset')
$guardianDispatchMethod = [regex]::Match(
    $normalizedGuardianCommunication,
    'private GuardianTeamCommunicationCommandOutcome DispatchOnce\(.*?\) \{(?<Body>.*?)\} private void CountOutcome')
$guardianObserveBody = $guardianObserveMethod.Groups['Body'].Value
$guardianDispatchBody = $guardianDispatchMethod.Groups['Body'].Value
if (-not $guardianObserveMethod.Success -or
    -not $guardianDispatchMethod.Success -or
    [regex]::Matches($guardianObserveBody, '\bDispatchOnce\s*\(').Count -ne 1 -or
    [regex]::Matches($guardianDispatchBody, '\bcommands\.Try(?:QuickChatCoveringTarget|MarkGuardianAlly|MarkGuardianSelf|ClearGuardianAlly|ClearGuardianSelf)\s*\(').Count -ne 5 -or
    $guardianDispatchBody -notmatch 'command\.IsValid.*?episode\.Token != command\.EpisodeToken.*?context != SupportedPvPContext\.CrystallineConflict.*?ResolveSupportedPvPContext\(\) != SupportedPvPContext\.CrystallineConflict.*?!TryGetTextInputState\(out var textInputActive\).*?textInputActive' -or
    $guardianDispatchBody -notmatch 'var exactLocal = ResolveExactLocal\(localPlayer\); if \(!exactLocal\.Exact \|\| exactLocal\.Actor != episode\.LocalPlayer\).*?TerminalFailure' -or
    $guardianDispatchBody -notmatch 'case GuardianTeamCommunicationCommandKind\.SetBind2:.*?!MatchesExactPartyTarget\(episode, command\).*?!bind1\.IsExactlyEmpty\(GuardianTeamCommunicationRules\.Bind1MarkerIndex\).*?!bind2\.IsExactlyEmpty\(GuardianTeamCommunicationRules\.Bind2MarkerIndex\).*?bind1\.MarkerTime != state\.Bind1MarkerTimeBeforeSet.*?bind2\.MarkerTime != state\.Bind2MarkerTimeBeforeSet.*?commands\.TryMarkGuardianAlly' -or
    $guardianDispatchBody -notmatch 'case GuardianTeamCommunicationCommandKind\.SetBind1:.*?command\.Actor != episode\.LocalPlayer.*?!exactTarget\.Exact.*?exactTarget\.Actor != episode\.Target.*?!bind1\.IsExactlyEmpty.*?!state\.OwnsBind2.*?bind2\.GameObjectId != episode\.Target\.GameObjectId.*?bind2\.MarkerTime != state\.Bind2OwnedMarkerTime.*?commands\.TryMarkGuardianSelf' -or
    $guardianDispatchBody -notmatch 'case GuardianTeamCommunicationCommandKind\.ClearBind2:.*?command\.Actor != episode\.Target.*?!MatchesExactPartyTarget\(episode, command\).*?!MarkerMatchesOwnedCommand\(bind2, command\).*?commands\.TryClearGuardianAlly' -or
    $guardianDispatchBody -notmatch 'case GuardianTeamCommunicationCommandKind\.ClearBind1:.*?command\.Actor != episode\.LocalPlayer.*?!MarkerMatchesOwnedCommand\(bind1, command\).*?commands\.TryClearGuardianSelf' -or
    $guardianDispatchBody -notmatch 'ReviewedPvpCommandDispatchResult\.Invoked => GuardianTeamCommunicationCommandOutcome\.Invoked, ReviewedPvpCommandDispatchResult\.MarkerRateLimited when command\.Kind != GuardianTeamCommunicationCommandKind\.SendQuickChat => GuardianTeamCommunicationCommandOutcome\.DeferredBeforeInvocation, _ => GuardianTeamCommunicationCommandOutcome\.TerminalFailure' -or
    [regex]::Matches($guardianCommunication, '\bunsafe\b').Count -ne 3 -or
    [regex]::Matches($guardianCommunication, '\bMarkingController\.Instance\s*\(').Count -ne 1 -or
    $guardianCommunication -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|Hook<|HookFromAddress|ITargetManager|TargetManager|SetTarget|RaptureShellModule|GetRaptureShellModule|ExecuteCommandInner|Utf8String\.FromString)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=|Markers\s*\[[^\]]+\]\s*=|MarkerTimes\s*\[[^\]]+\]\s*=') {
    throw 'Guardian runtime must revalidate exact CC/local/P-slot/text/config/metadata/marker ownership, issue at most one typed command per Observe tick, map only marker reservation to pre-invocation deferral, and never initiate combat, retarget, write marker memory, or own a raw shell boundary.'
}

# Scholar Critical Strategy freezes one exact S-slot intent. A clean client
# false may retry that same intent through the shared policy; acceptance can
# reuse the continuous hold only after a distinct observed cooldown epoch.
$scholarCriticalStrategyRules = Read-RequiredSource $scholarCriticalStrategyRulesPath 'Scholar Critical Strategy rules'
$normalizedScholarCriticalStrategyRules = $scholarCriticalStrategyRules -replace '\s+', ' '
$scholarCriticalStrategySelfTests = Read-RequiredSource $scholarCriticalStrategySelfTestsPath 'Scholar Critical Strategy self-tests'
Assert-Literals $scholarCriticalStrategyRules @(
    'public readonly record struct ScholarCriticalStrategyCandidate(',
    'public readonly record struct ScholarCriticalStrategyIntent(',
    'public readonly record struct ScholarCriticalStrategyObservation(',
    'public readonly record struct ScholarCriticalStrategyDecision(',
    'public bool ShouldConsumeInputGeneration => ShouldDispatch;',
    'public const uint ScholarJobId = 28;',
    'public const uint ActionId = 29_716;',
    'public const uint GuardStatusId = 3_054;',
    'public const uint GuardStatusLargeScaleId = 3_673;',
    'bool CompleteCanonicalEnemySet,',
    'bool HeldGameplayKeyEligible,',
    'bool GuardActive,',
    'bool NativeTargetValid,',
    'bool NativeRangeAndLineOfSight,',
    'bool PressureKnown,',
    'int TeamTargetCount',
    'candidates.Count != EnemySlotRules.LastSlot',
    '!occupiedSlots.Add(candidate.EnemySlot)',
    '!occupiedActors.Add(candidate.Actor)',
    'if (!occupiedSlots.Contains(slot)) return false;',
    'eligibleIndices.All(index =>',
    'candidates[index].PressureKnown &&',
    'candidates[index].TeamTargetCount >= 0',
    'eligibleIndices.Any(index =>',
    'candidates[index].TeamTargetCount > 0',
    'right.TeamTargetCount.CompareTo(left.TeamTargetCount)',
    'left.EnemySlot.CompareTo(right.EnemySlot)',
    'left.Actor.EntityId.CompareTo(right.Actor.EntityId)',
    'left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId)',
    'currentCandidate.EnemySlot == intent.EnemySlot',
    'currentCandidate.Actor == intent.Target',
    'IsEligibleCandidate(currentCandidate, currentLocal)'
) 'Pure SCH held-key guarded-target policy'
$scholarSelectionMethod = [regex]::Match(
    $normalizedScholarCriticalStrategyRules,
    'public static int SelectBestCandidateIndex\(.*?\) \{(?<Body>.*?)\} /// <summary> /// Revalidates only the frozen actor and action\..*?public static bool CanUseExactIntent')
$scholarFinalIntentMethod = [regex]::Match(
    $normalizedScholarCriticalStrategyRules,
    'public static bool CanUseExactIntent\(.*?\) =>(?<Body>.*?); private static ScholarCriticalStrategyDecisionReason GetGateFailure')
$scholarSelectionBody = $scholarSelectionMethod.Groups['Body'].Value
$scholarFinalIntentBody = $scholarFinalIntentMethod.Groups['Body'].Value
if (-not $scholarSelectionMethod.Success -or
    -not $scholarFinalIntentMethod.Success -or
    $normalizedScholarCriticalStrategyRules -notmatch 'if \(observation\.HardReset\).*?HardReset.*?if \(!observation\.ConfigurationEnabled\).*?ConfigurationDisabled.*?if \(!observation\.IsCrystallineConflict\).*?OutsideCrystallineConflict.*?if \(!observation\.LocalPlayer\.IsValid\).*?LocalPlayerIdentityInvalid.*?if \(!observation\.IsLocalPlayerAlive\).*?LocalPlayerDead.*?if \(observation\.LocalJobId != ScholarJobId\).*?LocalJobInvalid.*?if \(!observation\.MetadataVerified\).*?MetadataUnverified.*?if \(observation\.ActionHelpersSuppressedByGuard\).*?GuardSuppressed.*?if \(observation\.HigherPriorityClaimed\).*?HigherPriorityClaimed.*?if \(!observation\.InputProbeSucceeded\).*?InputProbeUnavailable.*?if \(observation\.IsTextInputActive\).*?TextInputActive.*?if \(!observation\.HeldGameplayKeyEligible\).*?NoHeldGameplayKey.*?if \(observation\.ResolvedActionId != ActionId\).*?ResolvedActionInvalid.*?if \(!observation\.ActionLocallyReady\).*?ActionNotReady.*?if \(!observation\.CompleteCanonicalEnemySet\).*?IncompleteCanonicalEnemySet' -or
    $normalizedScholarCriticalStrategyRules -notmatch 'candidate\.Alive && candidate\.Targetable && candidate\.CurrentHp > 0 && candidate\.MaximumHp > 0 && candidate\.CurrentHp <= candidate\.MaximumHp && candidate\.GuardActive && candidate\.NativeTargetValid && candidate\.NativeRangeAndLineOfSight' -or
    $scholarSelectionBody -notmatch 'var useTeamPressure = eligibleIndices\.All\(.*?PressureKnown.*?TeamTargetCount >= 0\) && eligibleIndices\.Any\(.*?TeamTargetCount > 0\).*?Compare\( candidates\[candidateIndex\], candidates\[bestIndex\], useTeamPressure\)' -or
    $scholarFinalIntentBody -match '\b(PressureKnown|TeamTargetCount|SelectBestCandidateIndex|Candidates)\b' -or
    $scholarFinalIntentBody -notmatch 'currentLocal == intent\.LocalPlayer.*?actionLocallyReady.*?resolvedActionId == intent\.ActionId.*?currentCandidate\.EnemySlot == intent\.EnemySlot.*?currentCandidate\.Actor == intent\.Target.*?IsEligibleCandidate\(currentCandidate, currentLocal\)' -or
    $scholarCriticalStrategyRules -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|HookFromAddress|ITargetManager|TargetManager|SetTarget|RaptureShellModule|ExecuteCommandInner|MarkingController|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'SCH Core must require default-off exact CC/SCH/held/action gates, a complete unique canonical set, live Guard and native reachability, wholesale positive pressure ranking with HP fallback, and pressure-free frozen-intent revalidation without dispatch or target access.'
}

$scholarSelfTestMethods = @(
    'CandidateEligibilityRequiresLiveGuardAndNativeReachability',
    'CompleteCanonicalSetIsExactAndUnique',
    'TrustedPositivePressureRanksBeforeExactHp',
    'UnknownOrAllZeroPressureFallsBackToHp',
    'DispatchRequiresEveryGateAndHeldGeneration',
    'DispatchFreezesOneIntentWithoutPressureRevalidation',
    'ConsumedHeldGenerationCannotRetry',
    'AcceptedHoldRepeatsOnlyAfterCooldownEpoch'
)
foreach ($method in $scholarSelfTestMethods) {
    Assert-Literals $scholarCriticalStrategySelfTests @("internal static void $method()") "Scholar Critical Strategy self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("ScholarCriticalStrategySelfTests.$method") "Scholar Critical Strategy test registration $method"
}
if ([regex]::Matches($scholarCriticalStrategySelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 8 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bScholarCriticalStrategySelfTests\.\w+').Count -ne 8) {
    throw 'All eight Scholar Critical Strategy gate, ranking, frozen-intent, and accepted-cooldown-epoch tests must remain registered exactly once.'
}

# The runtime keeps initial selection separate from the frozen retry boundary.
# Its retry and first-attempt branches are mutually exclusive, claim only the
# current frame, and use the same exact action/target without reranking.
$scholarCriticalStrategyProbe = Read-RequiredSource $scholarCriticalStrategyProbePath 'Scholar Critical Strategy runtime probe'
$normalizedScholarCriticalStrategyProbe = $scholarCriticalStrategyProbe -replace '\s+', ' '
Assert-Literals $scholarCriticalStrategyProbe @(
    'internal sealed record ScholarCriticalStrategyProbeSnapshot(',
    'bool PressureKnown,',
    'int TeamTargetCount,',
    'bool InputClaimed,',
    'bool UseActionAttempted,',
    'bool UseActionAccepted,',
    'string CandidateResolution,',
    'internal ScholarCriticalStrategyProbeSnapshot Observe(',
    'private ScholarCriticalStrategyHoldState acceptedHold = ScholarCriticalStrategyHoldState.Initial;',
    'private FrozenScholarRetry? frozenRetry;',
    'private VirtualKey terminalHeldKey = VirtualKey.NO_KEY;',
    'if (frozenRetry is { } retry)',
    'else if (terminalHeldKey == VirtualKey.NO_KEY &&',
    'HeldActionRetryRules.RetainsSchedulerFrame(',
    'HeldActionRetryRules.Complete(',
    'HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(',
    'ScholarCriticalStrategyRules.ObserveAcceptedHold(',
    'ScholarCriticalStrategyRules.BeginAcceptedHold(',
    'ScholarCriticalStrategyRules.TrySpendReadyEpoch(',
    'inputFrame.Consume();',
    'TryUseCriticalStrategyOnce(',
    'ClientActionAttemptBoundary.Capture(',
    'ClientActionAttemptBoundaryRules.Classify(',
    'nearAssist.RunWithoutRedirect',
    'actionManager->UseAction(',
    'intent.ActionId,',
    'intent.Target.GameObjectId,'
) 'Exact retryable SCH Critical Strategy runtime and truthful diagnostics'
if ([regex]::Matches($scholarCriticalStrategyProbe, '\bUseAction\s*\(').Count -ne 1 -or
    [regex]::Matches($scholarCriticalStrategyProbe, '\bClientActionAttemptBoundary\.Capture\s*\(').Count -ne 2 -or
    [regex]::Matches($scholarCriticalStrategyProbe, '\bClientActionAttemptBoundaryRules\.Classify\s*\(').Count -ne 1 -or
    [regex]::Matches($scholarCriticalStrategyProbe, '\bHeldActionRetryRules\.Complete\s*\(').Count -ne 1 -or
    $normalizedScholarCriticalStrategyProbe -notmatch 'if \(frozenRetry is \{ \} retry\).*?else if \(terminalHeldKey == VirtualKey\.NO_KEY && decision\.ShouldDispatch' -or
    $scholarCriticalStrategyProbe -match '\b(IGameInteropProvider|Hook<|HookFromAddress|SignatureAttribute|SigScanner|ITargetManager|TargetManager|SetTarget|ResolvePlaceholder|RaptureShellModule|ExecuteCommandInner|MarkingController)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=|(?:->|\.)Original\s*\(') {
    throw 'SCH must keep one exact direct-GOID native boundary, mutually exclusive first/retry branches, shared outcome classification, and no hook, alternate, replay, or target mutation.'
}

Assert-Literals $pluginSource @(
    'personalStatus.ScholarCriticalStrategyDiagnostics',
    '[Seiton Sense] scholar-strategy[decision={scholar.Decision},reason={scholar.Reason}',
    'ready={scholar.LocallyReady},action={scholar.ResolvedActionId}',
    'candidates={scholar.CandidateCount},S={scholar.EnemySlot}',
    'target={scholar.TargetGameObjectId:X}/{scholar.TargetEntityId:X}',
    'pressure={scholar.PressureKnown}/{scholar.TeamTargetCount}',
    'held={scholar.HeldGameplayKey},claimed={scholar.InputClaimed}',
    'attempt={scholar.UseActionAttempted}/{scholar.UseActionAccepted}',
    'count={scholar.AttemptCount}/{scholar.AcceptedCount}',
    'resolve={scholar.CandidateResolution},last={scholar.LastEvent}'
) 'Truthful SCH Critical Strategy source diagnostics'
$scholarDebugStart = $pluginSource.IndexOf('[Seiton Sense] scholar-strategy[')
$scholarDebugEnd = if ($scholarDebugStart -ge 0) {
    $pluginSource.IndexOf('[Seiton Sense] monk-reply[', $scholarDebugStart)
} else {
    -1
}
if ($scholarDebugStart -lt 0 -or $scholarDebugEnd -le $scholarDebugStart -or
    $pluginSource.Substring($scholarDebugStart, $scholarDebugEnd - $scholarDebugStart) -match '(?i)\b(landed|killed|executed successfully|server accepted)\b') {
    throw 'SCH diagnostics may report only attempted/client-accepted telemetry, never a landed effect, Guard change, or kill.'
}

# Smart Kardia is event-driven. Only one client-accepted incoming PvP Eukrasia
# call may open a two-second opportunity. Charge loss or a newly appeared exact
# own-source status must causally confirm it, and the helper consumes the token
# before at most one direct-GOID Kardia request. There is no held-key scan or
# independent six-second throttle.
$smartKardiaRules = Read-RequiredSource $smartKardiaRulesPath 'Smart Kardia rules'
$normalizedSmartKardiaRules = $smartKardiaRules -replace '\s+', ' '
$smartKardiaProbe = Read-RequiredSource $smartKardiaProbePath 'Smart Kardia runtime probe'
$normalizedSmartKardiaProbe = $smartKardiaProbe -replace '\s+', ' '
$smartKardiaSelfTests = Read-RequiredSource $smartKardiaSelfTestsPath 'Smart Kardia self-tests'
$smartKardiaMetadata = Read-RequiredSource (
    Join-Path $pluginServicesRoot 'SeitonMetadataGuard.cs') 'Smart Kardia metadata guard'
$normalizedNearAssist = (Read-RequiredSource $nearAssistPath 'Near Assist shared hook') -replace '\s+', ' '

Assert-Literals $smartKardiaRules @(
    'public readonly record struct SmartKardiaEukrasiaEvidence(',
    'public readonly record struct SmartKardiaEukrasiaTrigger(',
    'public readonly record struct SmartKardiaIntent(',
    'public const uint SageJobId = 40;',
    'public const uint ActionId = 29_264;',
    'public const uint EukrasiaActionId = 29_258;',
    'public const uint KardiaStatusId = 2_871;',
    'public const uint KardionStatusId = 2_872;',
    'public const uint EukrasiaStatusId = 3_107;',
    'public const int MinimumIncomingEnemyCount = 2;',
    'public const uint EukrasiaMaximumCharges = 2;',
    'public const long TriggerLifetimeMilliseconds = 2_000;',
    'public static bool TryCreateAcceptedTrigger(',
    'public static bool IsTriggerCurrent(',
    'public static bool HasCausalEukrasiaEvidence(',
    'current.CurrentCharges < trigger.Before.CurrentCharges',
    '!trigger.Before.HasOwnEukrasia && current.HasOwnEukrasia',
    'SmartKardiaDecisionReason.EukrasiaTriggerUnavailable',
    'SmartKardiaDecisionReason.EukrasiaEvidencePending',
    'SmartKardiaDecisionReason.PressurePublicationPending',
    'SmartKardiaDecisionReason.AnimationLockActive',
    'public static bool IsEligibleSelfFallback(',
    'public static int SelectBestCandidateIndex(',
    'if (bestIndex >= 0) return bestIndex;',
    'currentCandidate.OwnKardionStateKnown &&',
    '!currentCandidate.HasOwnKardion;'
) 'Accepted-Eukrasia Smart Kardia policy'
if ($normalizedSmartKardiaRules -notmatch 'acceptedAtMilliseconds >= 0 && ExpiresAtMilliseconds > AcceptedAtMilliseconds.*?Before\.IsValid && Before\.CurrentCharges > 0' -or
    $normalizedSmartKardiaRules -notmatch 'acceptedAtMilliseconds > long\.MaxValue - TriggerLifetimeMilliseconds \? long\.MaxValue : acceptedAtMilliseconds \+ TriggerLifetimeMilliseconds' -or
    $normalizedSmartKardiaRules -notmatch 'nowMilliseconds >= trigger\.AcceptedAtMilliseconds && nowMilliseconds < trigger\.ExpiresAtMilliseconds && trigger\.TerritoryId == territoryId && trigger\.LocalPlayer == localPlayer' -or
    $normalizedSmartKardiaRules -notmatch 'if \(!observation\.TriggerAvailable\).*?EukrasiaTriggerUnavailable.*?if \(!observation\.TriggerEvidenceConfirmed\).*?EukrasiaEvidencePending.*?if \(!observation\.FreshPressurePublicationAvailable\).*?PressurePublicationPending.*?if \(observation\.ResolvedActionId != ActionId\).*?ResolvedActionInvalid.*?if \(!observation\.ActionLocallyReady\).*?ActionNotReady.*?if \(!observation\.AnimationLockClear\).*?AnimationLockActive' -or
    $normalizedSmartKardiaRules -notmatch 'if \(!HasCompleteKnownPressureView\(observation\.Candidates\)\).*?IncompleteKnownPressureView.*?SelectBestCandidateIndex.*?if \(!candidate\.OwnKardionStateKnown\).*?SelectedKardionStateUnknown.*?if \(candidate\.HasOwnKardion\).*?SelectedAlreadyHasOwnKardion.*?new SmartKardiaIntent\(' -or
    $normalizedSmartKardiaRules -notmatch 'if \(!HasCompleteExactPartyView\(candidates, localPlayer\) \|\| !HasCompleteKnownPressureView\(candidates\)\).*?IsEligibleCandidate\(candidate, localPlayer\).*?Compare\(candidate, candidates\[bestIndex\]\).*?if \(bestIndex >= 0\) return bestIndex;.*?IsEligibleSelfFallback\(candidates\[index\], localPlayer\).*?return index;.*?return -1;' -or
    $smartKardiaRules -match '\b(?:HeldGameplayKey|InputProbe|MinimumAttemptInterval|AttemptWindow|nextAttemptAllowed|InputClaimed|ConsumeInput)\b') {
    throw 'Smart Kardia Core must remain a bounded accepted-Eukrasia event policy with causal evidence, fresh pressure, deterministic 2+ ranking, exact self fallback, and no held-key or six-second throttle state.'
}

$smartKardiaTestMethods = @(
    'ExactIdsAndCandidateEligibilityArePinned',
    'CompletePartyViewRejectsIdentityAmbiguity',
    'PartialLivePressureViewFailsClosed',
    'RankingIsPressureThenExactHpThenStableSlot',
    'BestKardionStateNeverFallsThroughToAnAlternate',
    'DefaultSelfFallbackIsExactAndTerminal',
    'AcceptedTriggerIsBoundedAndIdentityExact',
    'CausalEvidenceRequiresChargeOrOwnedStatusTransition',
    'DispatchRequiresEveryEventAndSafetyGate',
    'FrozenIntentCannotRerankFallbackOrRetry'
)
foreach ($method in $smartKardiaTestMethods) {
    Assert-Literals $smartKardiaSelfTests @("internal static void $method()") "Smart Kardia self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("SmartKardiaSelfTests.$method") "Smart Kardia test registration $method"
}
if ([regex]::Matches($smartKardiaSelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 10 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bSmartKardiaSelfTests\.\w+').Count -ne 10) {
    throw 'All ten accepted-trigger, causal-evidence, party, pressure, fallback, and frozen-intent Smart Kardia tests must remain registered exactly once.'
}

Assert-Literals $smartKardiaProbe @(
    'never scans continuously while idle',
    'bool TriggerConsumed,',
    'nearAssist.TryPeekSmartKardiaTrigger(',
    'SmartKardiaRules.HasCausalEukrasiaEvidence(',
    'pressurePublishedAt >= trigger.AcceptedAtMilliseconds',
    'pressurePublishedAt <= nowMilliseconds',
    'out animationLockClear',
    'var selectionEvaluated = shouldResolveCandidates && capture.Complete;',
    'nearAssist.TryConsumeSmartKardiaTrigger(trigger.Token)',
    'TryUseKardiaOnce(',
    'configuration.EnableSageKardiaAfterEukrasia',
    'SmartKardiaRules.IsTriggerCurrent(',
    'trigger.AcceptedAtMilliseconds',
    'SmartKardiaRules.CanUseFrozenIntent(',
    'intent.Target.GameObjectId,',
    'attempt failed and will not be retried.'
) 'One-shot accepted-Eukrasia Smart Kardia runtime'
if ($normalizedSmartKardiaProbe -notmatch 'var triggerAvailable = featureContextReady && localIdentity\.IsValid && nearAssist\.TryPeekSmartKardiaTrigger\( nowMilliseconds, clientState\.TerritoryType, localIdentity, out trigger\);.*?SmartKardiaRules\.HasCausalEukrasiaEvidence\( trigger, currentEvidence\);.*?pressurePublishedAt >= trigger\.AcceptedAtMilliseconds && pressurePublishedAt <= nowMilliseconds' -or
    $normalizedSmartKardiaProbe -notmatch 'var shouldResolveCandidates = featureContextReady && triggerAvailable && triggerEvidenceConfirmed && freshPressurePublicationAvailable && actionReady && animationLockClear && !higherPriorityClaimed;.*?CaptureExactParty\( localPlayer!, resolvedActionId, trigger\.AcceptedAtMilliseconds, nowMilliseconds' -or
    $normalizedSmartKardiaProbe -notmatch 'var selectionEvaluated = shouldResolveCandidates && capture\.Complete; var triggerConsumed = selectionEvaluated && nearAssist\.TryConsumeSmartKardiaTrigger\(trigger\.Token\);.*?if \(selectionEvaluated && !triggerConsumed\).*?EukrasiaTriggerUnavailable' -or
    $normalizedSmartKardiaProbe -notmatch 'private bool TryUseKardiaOnce\(.*?var boundaryNow = Environment\.TickCount64;.*?SmartKardiaRules\.IsTriggerCurrent\( trigger, boundaryNow, clientState\.TerritoryType, intent\.LocalPlayer\).*?configuration\.EnableSageKardiaAfterEukrasia.*?TryReadExactEukrasiaEvidence.*?SmartKardiaRules\.HasCausalEukrasiaEvidence.*?ResolveFrozenCandidate\(.*?trigger\.AcceptedAtMilliseconds, boundaryNow\).*?SmartKardiaRules\.CanUseFrozenIntent\(.*?attempted = true; return nearAssist\.RunWithoutRedirect\(\(\) => actionManager->UseAction\(' -or
    [regex]::Matches($smartKardiaProbe, '\bUseAction\s*\(').Count -ne 1 -or
    $smartKardiaProbe -match '\b(?:HeldGameplayKey|EmergencyActionInputFrame|MinimumAttemptInterval|AttemptWindow|nextAttemptAllowed|inputFrame\.Consume)\b' -or
    $smartKardiaProbe -match '\b(IGameInteropProvider|Hook<|HookFromAddress|ITargetManager|TargetManager|SetTarget|RaptureShellModule|ExecuteCommandInner|MarkingController)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=' -or
    $smartKardiaProbe -cmatch '\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction|PendingDispatch|BufferedDispatch)\b') {
    throw 'Smart Kardia runtime must wait on one accepted trigger, causal state transition, fresh pressure and animation unlock; consume before one direct-GOID request; and never scan held input, throttle independently, retarget, queue, alternate, or retry.'
}

Assert-Literals $normalizedNearAssist @(
    'TryCaptureSmartKardiaEukrasiaPreflight(',
    'ResolveActionId(actionManager, actionType, actionId) != SmartKardiaRules.EukrasiaActionId',
    'incomingTargetId != forwardedTargetId',
    'var clientAccepted = useActionHook!.Original( thisPtr, actionType, actionId, forwardedTargetId, extraParam, mode, comboRouteId, outOptAreaTargeted);',
    'if (clientAccepted && hasSmartKardiaPreflight) ArmAcceptedSmartKardiaTrigger(smartKardiaPreflight);',
    'pendingSmartKardiaTrigger = trigger;',
    'pressureTracker.RequestIncomingAllyPressureCapture(acceptedAt);',
    'pressureTracker.CancelIncomingAllyPressureCapture( acceptedAtMilliseconds);',
    'mode is ActionManager.UseActionMode.None or ActionManager.UseActionMode.Macro',
    '(uint)mode == 100'
) 'Sole-hook accepted Eukrasia trigger ownership'
if ([regex]::Matches((Read-RequiredSource $nearAssistPath 'Near Assist shared hook'), 'useActionHook!\.Original\s*\(').Count -ne 1 -or
    $normalizedNearAssist -notmatch 'before\.CurrentCharges == 0.*?return false;.*?preflight = new SmartKardiaEukrasiaPreflight' -or
    $normalizedNearAssist -notmatch 'if \(!configuration\.Enabled \|\| !configuration\.EnableSageKardiaAfterEukrasia \|\| pendingSmartKardiaTrigger is \{ \} pending && SmartKardiaRules\.IsTriggerCurrent.*?return;.*?pendingSmartKardiaTrigger = null;.*?NextSmartKardiaTriggerToken\(\).*?SmartKardiaRules\.TryCreateAcceptedTrigger.*?pendingSmartKardiaTrigger = trigger;.*?pressureTracker\.RequestIncomingAllyPressureCapture\(acceptedAt\);') {
    throw 'The sole existing UseAction detour must forward Eukrasia unchanged, arm only after a true client return with exact preflight evidence, reject overlapping live tokens, and request one fresh pressure publication.'
}

Assert-Literals $smartKardiaMetadata @(
    'ValidateFeature("Smart Kardia"',
    'SmartKardiaRules.ActionId',
    'SmartKardiaRules.EukrasiaActionId',
    'SmartKardiaRules.KardiaStatusId',
    'SmartKardiaRules.KardionStatusId',
    'SmartKardiaRules.EukrasiaStatusId',
    'string.Equals(action.Name.ToString(), "Kardia", StringComparison.Ordinal)',
    'action.Range == SmartKardiaProbe.ExpectedRange',
    'action.CanTargetSelf',
    'action.CanTargetParty',
    '!action.CanTargetHostile',
    'action.RequiresLineOfSight',
    'kardia.IsPermanent',
    'kardion.IsPermanent',
    'eukrasiaAction.MaxCharges ==',
    'SmartKardiaRules.EukrasiaMaximumCharges',
    'eukrasiaAction.CanTargetSelf',
    '!eukrasiaAction.CanTargetParty',
    'eukrasiaStatus.Icon == SmartKardiaProbe.EukrasiaStatusIconId',
    '!eukrasiaStatus.IsPermanent',
    'Certain actions are being augmented'
) 'Exact local SqPack PvP Kardia and Eukrasia metadata'
if (($smartKardiaRules + $smartKardiaProbe + $smartKardiaMetadata) -match '\b(?:2_604|2_605|2_606|2604|2605|2606)\b') {
    throw 'Production Smart Kardia paths must never accept PvE Kardia, Kardion, or Eukrasia rows 2604/2605/2606.'
}

# Smart Recuperate is the shared held-key self-heal. It soft-waits without
# spending native budget, retries only a proven clean false, and after accepted
# use requires a real unavailable-to-ready cooldown epoch before the same hold
# may authorize a distinct health event.
$smartRecuperateRules = Read-RequiredSource $smartRecuperateRulesPath 'Smart Recuperate rules'
$normalizedSmartRecuperateRules = $smartRecuperateRules -replace '\s+', ' '
$smartRecuperateProbe = Read-RequiredSource $smartRecuperateProbePath 'Smart Recuperate runtime probe'
$normalizedSmartRecuperateProbe = $smartRecuperateProbe -replace '\s+', ' '
$smartRecuperateSelfTests = Read-RequiredSource $smartRecuperateSelfTestsPath 'Smart Recuperate self-tests'
Assert-Literals $smartRecuperateRules @(
    'public readonly record struct SmartRecuperateIntent(',
    'public readonly record struct SmartRecuperateState(',
    'public readonly record struct SmartRecuperateObservation(',
    'HeldActionRetryState Retry,',
    'public bool ShouldConsumeInputGeneration => InputClaimed;',
    'public const uint ActionId = 29_711;',
    'public const uint MinimumMissingHp = 16_000;',
    'public const uint MpCost = 2_000;',
    '(ulong)maximumHp - currentHp >= MinimumMissingHp;',
    'currentMp >= MpCost;',
    'SmartRecuperatePhase.WaitingForAcceptedCooldownUnavailable',
    'SmartRecuperatePhase.WaitingForAcceptedCooldownReady',
    'HeldActionRetryRules.CanAttemptFrozenIntent(',
    'HeldActionRetryRules.Complete(',
    'public static SmartRecuperateNativeAttemptDecision ApplyNativeAttemptOutcome(',
    'SmartRecuperateDecisionReason.MissingHealthBelowThreshold',
    'SmartRecuperateDecisionReason.InsufficientMp',
    'public static bool CanUseFrozenIntent('
) 'Exact inclusive and retryable Smart Recuperate policy'
if ($smartRecuperateRules -match '\b(?:TargetManager|PartySlot|Candidate|PressureTracker|QueueAction|PendingDispatch|BufferedDispatch|Timer|Stopwatch)\b') {
    throw 'Smart Recuperate Core must remain exact self-only policy with no target selection, alternate, queue, or timer ownership.'
}
$smartRecuperateTestMethods = @(
    'ExactIdsAndInclusiveThresholdsArePinned',
    'MpTickWaitDoesNotConsumeTheHold',
    'EveryInitialSafetyGateFailsClosed',
    'FrozenIntentRequiresEveryTerminalGate',
    'CleanFalseRetriesAreBounded',
    'SoftUnavailableIsFreeAndAcceptedCooldownDefinesRepeat',
    'PurifyPriorityNeverGetsStarved'
)
foreach ($method in $smartRecuperateTestMethods) {
    Assert-Literals $smartRecuperateSelfTests @("public static void $method()") "Smart Recuperate self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("SmartRecuperateSelfTests.$method") "Smart Recuperate test registration $method"
}
if ([regex]::Matches($smartRecuperateSelfTests, '\bpublic static void\s+\w+\s*\(').Count -ne 7 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bSmartRecuperateSelfTests\.\w+').Count -ne 7) {
    throw 'All seven Smart Recuperate threshold, frozen-intent, retry, cooldown-epoch, and Purify-priority tests must remain registered exactly once.'
}
Assert-Literals $smartRecuperateProbe @(
    'var inputClaimed = decision.ShouldConsumeInputGeneration;',
    'if (inputClaimed) inputFrame.Consume();',
    'TryUseRecuperate(',
    'SmartRecuperateRules.Observe(',
    'SmartRecuperateRules.ApplyNativeAttemptOutcome(',
    'SmartRecuperateRules.CanUseFrozenIntent(',
    'HeldActionRetryRules.IsNativeBoundaryNearQueueable(',
    'ClientActionAttemptBoundary.Capture(',
    'ClientActionAttemptBoundaryRules.Classify(',
    'intent.LocalPlayer.GameObjectId,',
    'Recuperate client-accepted; awaiting cooldown epoch',
    'Recuperate client-rejected; exact intent retained for bounded retry',
    'Recuperate waiting without spending retry budget'
) 'Shared-policy Smart Recuperate runtime'
if ($normalizedSmartRecuperateProbe -notmatch 'var inputClaimed = decision\.ShouldConsumeInputGeneration; if \(inputClaimed\) inputFrame\.Consume\(\);.*?if \(decision\.ShouldDispatch && decision\.Intent is \{ \} intent\).*?TryUseRecuperate' -or
    [regex]::Matches($smartRecuperateProbe, '\bUseAction\s*\(').Count -ne 1 -or
    [regex]::Matches($smartRecuperateProbe, '\bClientActionAttemptBoundary\.Capture\s*\(').Count -lt 2 -or
    [regex]::Matches($smartRecuperateProbe, '\bClientActionAttemptBoundaryRules\.Classify\s*\(').Count -ne 1 -or
    [regex]::Matches($smartRecuperateProbe, '\binputFrame\.Consume\s*\(').Count -ne 1 -or
    $smartRecuperateProbe -match '\b(?:ITargetManager|TargetManager|SetTarget|Hook<|HookFromAddress|QueueAction|PendingDispatch)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Smart Recuperate must claim only dispatchable frames and issue one exact frozen self-GOID boundary using shared classification, with no retarget, alternate, or replay.'
}

Assert-Literals $smartKardiaMetadata @(
    'ValidateFeature("Recuperate"',
    'EnemyCombatConstants.RecuperateActionId',
    'string.Equals(recuperate.Name.ToString(), "Recuperate", StringComparison.Ordinal)',
    'recuperate.Icon == EnemyCombatConstants.RecuperateIconId',
    'recuperate.IsPvP',
    'recuperate.IsPlayerAction',
    'recuperate.ClassJob.RowId == 0',
    'recuperate.ClassJobCategory.RowId == 85',
    'recuperate.ActionCategory.RowId == 4',
    'recuperate.Range == 0',
    'recuperate.EffectRange == 0',
    'recuperate.Recast100ms == 10',
    'recuperate.PrimaryCostType == 51',
    'recuperate.PrimaryCostValue == EnemyCombatConstants.RecuperateMpCost',
    'recuperate.CooldownGroup == 29',
    'recuperate.CanTargetSelf',
    '!recuperate.CanTargetParty',
    '!recuperate.CanTargetHostile',
    'recuperate.RequiresLineOfSight',
    'recuperate.NeedToFaceTarget',
    'recuperate.PreservesCombo',
    'Restores own HP.',
    'Cure Potency: 16,000'
) 'Exact installed PvP Recuperate action metadata'
if ($smartKardiaMetadata -match '!\s*recuperate\.ClassJob\.IsValid') {
    throw 'Recuperate metadata must accept the installed all-jobs RowRef and pin its semantic ClassJob RowId 0 sentinel.'
}

Assert-Literals $pluginSource @(
    '[Seiton Sense] smart-recuperate[decision={recuperate.Decision}',
    'missing={recuperate.MissingHp}',
    'mp={recuperate.CurrentMp}/{recuperate.MaximumMp}',
    'claimed={recuperate.InputClaimed}',
    '[Seiton Sense] smart-kardia[decision={kardia.Decision},reason={kardia.Reason}',
    'trigger-consumed={kardia.TriggerConsumed}',
    'attempt={kardia.UseActionAttempted}/{kardia.UseActionAccepted}'
) 'Truthful Smart Recuperate and accepted-trigger Kardia diagnostics'
if ($pluginSource -match '\[Seiton Sense\] smart-kardia\[[^\r\n]*(?:held|six-second|6000)') {
    throw 'Smart Kardia diagnostics must not describe the removed held-key or six-second design.'
}

# Warning audio is restricted to one shared, bounded client-owned chat-sound
# boundary. MCH and high-pressure episode services may delegate to it; external
# audio libraries, audio-file reads, URLs, and any second native path fail the build.
$warningSound = Read-RequiredSource $machinistLimitBreakWarningSoundPath 'Machinist limit-break warning sound'
$highPressureWarningSound = Read-RequiredSource $highPressureWarningSoundPath 'High-pressure warning sound'
Assert-Literals $warningSound @(
    'ThreatCooldownMilliseconds = 2_000',
    'PreviewCooldownMilliseconds = 350',
    'threatToken == 0',
    'threatToken == lastThreatToken',
    'lastThreatToken = threatToken',
    'nextThreatSoundAt = SaturatingAdd(nowMilliseconds, ThreatCooldownMilliseconds)',
    'soundId is < 1 or > 16',
    'UIGlobals.PlayChatSoundEffect((uint)soundId)',
    'MCH warning sound failed closed'
) 'Bounded shared FFXIV warning-sound boundary'
if ([regex]::Matches($warningSound, '\bUIGlobals\.PlayChatSoundEffect\s*\(').Count -ne 1) {
    throw 'The shared FFXIV warning-sound service must contain exactly one client-owned PlayChatSoundEffect call.'
}
$consumeThreatToken = [regex]::Match($warningSound, '\blastThreatToken\s*=\s*threatToken\s*;')
$playThreatSound = [regex]::Match($warningSound, '\breturn\s+TryPlay\s*\(\s*soundId\s*\)\s*;')
if (-not $consumeThreatToken.Success -or -not $playThreatSound.Success -or
    $consumeThreatToken.Index -gt $playThreatSound.Index) {
    throw 'MCH threat sound must consume its one-shot token before the native sound request.'
}
if ($warningSound -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event)\b') {
    throw 'Shared FFXIV warning audio must never initiate actions or mutate input/targets.'
}
Assert-Literals $highPressureWarningSound @(
    'EpisodeCooldownMilliseconds = 3_000',
    'PreviewCooldownMilliseconds = 350',
    'episodeToken == 0',
    'episodeToken == consumedEpisodeToken',
    'consumedEpisodeToken = episodeToken',
    'if (nowMilliseconds < nextEpisodeSoundAt) return false',
    'nextEpisodeSoundAt = SaturatingAdd(nowMilliseconds, EpisodeCooldownMilliseconds)',
    'MachinistLimitBreakWarningSound.TryPlayShared(',
    'high-pressure warning sound failed closed',
    'high-pressure warning sound preview failed closed'
) 'One-shot high-pressure FFXIV system sound'
$consumePressureEpisodeToken = [regex]::Match($highPressureWarningSound, '\bconsumedEpisodeToken\s*=\s*episodeToken\s*;')
$pressureEpisodeCooldown = [regex]::Match($highPressureWarningSound, '\bif\s*\(\s*nowMilliseconds\s*<\s*nextEpisodeSoundAt\s*\)\s*return\s+false\s*;')
$playPressureEpisodeSound = [regex]::Match($highPressureWarningSound, '\breturn\s+MachinistLimitBreakWarningSound\.TryPlayShared\s*\(')
if (-not $consumePressureEpisodeToken.Success -or -not $pressureEpisodeCooldown.Success -or
    -not $playPressureEpisodeSound.Success -or
    $consumePressureEpisodeToken.Index -gt $pressureEpisodeCooldown.Index -or
    $consumePressureEpisodeToken.Index -gt $playPressureEpisodeSound.Index) {
    throw 'High-pressure sound must consume each exact episode before cooldown/native evaluation so rejection or failure cannot retry it.'
}
if ($highPressureWarningSound -match '\b(UIGlobals|UseAction|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event)\b') {
    throw 'High-pressure sound may only delegate to the shared FFXIV sound boundary and may never initiate actions or mutate input/targets.'
}
$soundApiMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(UIGlobals\.PlayChatSoundEffect|SoundPlayer|MediaPlayer|PlaySound|sndPlaySound|NAudio|FMOD|XAudio2|AudioClient|WaveOut|WasapiOut)\b')
$unexpectedSoundApis = @($soundApiMatches | Where-Object {
    $_.Path -ne $machinistLimitBreakWarningSoundPath -or
    $_.Line -notmatch '\bUIGlobals\.PlayChatSoundEffect\s*\(\s*\(uint\)soundId\s*\)'
})
if ($unexpectedSoundApis.Count -gt 0) {
    $locations = $unexpectedSoundApis | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only the exact shared client-owned FFXIV chat-sound call is permitted: $($locations -join ', ')"
}
$externalAudioMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(File\.(?:ReadAllBytes(?:Async)?|ReadAllText(?:Async)?|OpenRead|Open)|FileStream|SoundPlayer|MediaPlayer|PlaySound|sndPlaySound|NAudio|FMOD|XAudio2|AudioClient|WaveOut|WasapiOut|DllImport|LibraryImport|NativeLibrary\.Load|Process\.Start)\b|https?://')
if ($externalAudioMatches.Count -gt 0) {
    $locations = $externalAudioMatches | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "External audio files, URLs, and playback libraries are forbidden: $($locations -join ', ')"
}

$mchCapture = Read-RequiredSource $machinistLimitBreakCapturePath 'Machinist limit-break capture'
Assert-Literals $mchCapture @(
    'Hook<ActionEffectHandler.Delegates.Receive>',
    'ActionEffectHandler.MemberFunctionPointers.Receive',
    'EffectSlotsPerTarget = 8',
    'MachinistLimitBreakMarkerRules.IsExactEarlyTargetMarker',
    'header->NumTargets != 1',
    'targetEntityIds[0].ObjectId == localEntityId',
    'finally',
    'OriginalDisposeSafe',
    'ConcurrentQueue<MachinistLimitBreakWarning>',
    'MaximumQueuedWarnings = 64',
    'ConcurrentQueue<TargetPressureCaptureEvent>',
    'MaximumQueuedPressureEvents = 128',
    'ConcurrentQueue<AllyRescueCleanseEffect>',
    'MaximumQueuedAllyRescueCleanses = 64',
    'ConcurrentQueue<MiracleInterceptThreatEvent>',
    'MaximumQueuedMiracleInterceptThreats = 64',
    'ConcurrentQueue<MiracleInterceptLandedEffect>',
    'MaximumQueuedMiracleInterceptConfirmations = 64',
    'SetAllyRescueLocalEntityId',
    'CurrentAllyRescueLocalEntityId',
    'TryCaptureAllyRescueCleanse',
    'RemoveStatusEffectType = 0x10',
    'casterEntityId != localEntityId',
    'actionId is not (WardensPaeanActionId or AquaveilActionId)',
    'IsPurifyRemovableStatus(effect.Value)',
    'if (depth > MaximumQueuedAllyRescueCleanses)',
    'DroppedAllyRescueCleanses',
    'SetMiracleInterceptLocalEntityId',
    'CurrentMiracleInterceptLocalEntityId',
    'SetMiracleCleanseFollowupLocalEntityId',
    'CurrentMiracleCleanseFollowupLocalEntityId',
    'CurrentMiracleCleanseFollowupGeneration',
    'miracleCleanseFollowupGeneration',
    'TryCaptureMiracleInterceptThreat',
    'EnemyCombatConstants.MarksmanSpiteActionId',
    'EnemyCombatConstants.ZantetsukenActionId',
    'EnemyCombatConstants.FuriousBacklashActionId',
    'EnemyCombatConstants.ContradanceActionId',
    'EnemyCombatConstants.PurifyActionId',
    'MiracleInterceptRules.ClassifyExactStartSignal(',
    'MiracleCleanseFollowupRules.IsExactPurifySignal(',
    'header->AnimationVariation',
    'IsEmpty(targetEffects[0])',
    'HasOnlyEmptyAdditionalEffects(targetEffects)',
    'if (depth > MaximumQueuedMiracleInterceptThreats)',
    'DroppedMiracleInterceptThreats',
    'TryCaptureMiracleInterceptConfirmation',
    'MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId)',
    'MiracleInterceptConfirmationRules.AddStatusEffectType',
    'if (depth > MaximumQueuedMiracleInterceptConfirmations)',
    'DroppedMiracleInterceptConfirmations',
    'SetPressureLocalEntityId',
    'TryCapturePressure',
    'HasHarmfulPressureEffect',
    'pressureEvent.TargetEntityId != CurrentPressureLocalEntityId'
) 'Read-only shared MCH LB, Ally Rescue confirmation, Miracle threat, and pressure ActionEffect capture'
if ([regex]::Matches($mchCapture, '\bHookFromAddress\s*\(').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bHook<ActionEffectHandler\.Delegates\.Receive>').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bOriginalDisposeSafe\s*\(').Count -ne 1 -or
    $mchCapture -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|SetTarget|TargetManager|SendInput|keybd_event|mouse_event)\b') {
    throw 'The shared capture must own exactly one ActionEffect hook, call its original exactly once, and never initiate an action or change input/targets.'
}
$boundedCaptureQueues = @(
    @('MachinistLimitBreakWarning', 'MaximumQueuedWarnings', '64', 'TryDequeue'),
    @('AllyRescueCleanseEffect', 'MaximumQueuedAllyRescueCleanses', '64', 'TryDequeueAllyRescueCleanse'),
    @('MiracleInterceptThreatEvent', 'MaximumQueuedMiracleInterceptThreats', '64', 'TryDequeueMiracleInterceptThreat'),
    @('MiracleInterceptLandedEffect', 'MaximumQueuedMiracleInterceptConfirmations', '64', 'TryDequeueMiracleInterceptConfirmation'),
    @('TargetPressureCaptureEvent', 'MaximumQueuedPressureEvents', '128', 'TryDequeuePressure')
)
foreach ($queue in $boundedCaptureQueues) {
    if ([regex]::Matches($mchCapture, "\bConcurrentQueue<$([regex]::Escape($queue[0]))>").Count -ne 1 -or
        [regex]::Matches($mchCapture, "\b$([regex]::Escape($queue[1]))\s*=\s*$($queue[2])\b").Count -ne 1 -or
        [regex]::Matches($mchCapture, "\bpublic\s+bool\s+$([regex]::Escape($queue[3]))\s*\(").Count -ne 1) {
        throw "Shared ActionEffect queue $($queue[0]) must have exactly one reviewed bounded queue, limit $($queue[2]), and dequeue boundary."
    }
}
$normalizedMchCapture = $mchCapture -replace '\s+', ' '
if ($normalizedMchCapture -notmatch 'finally \{ actionEffectHook!\.OriginalDisposeSafe\( casterEntityId, casterPointer, targetPosition, header, effects, targetEntityIds\); \} if \(capturedWarning is \{ \} warning\) Enqueue\(warning\);') {
    throw 'The sole shared ActionEffect Original must run in finally exactly once before any captured event is enqueued.'
}
if ($normalizedMchCapture -notmatch 'actionId is not \(WardensPaeanActionId or AquaveilActionId\)' -or
    $normalizedMchCapture -notmatch 'effect\.Type != RemoveStatusEffectType \|\| !IsPurifyRemovableStatus\(effect\.Value\)' -or
    $normalizedMchCapture -notmatch 'statusId is 1343 or 1344 or 1345 or 1347 or 3085 or 3219;') {
    throw 'Ally Rescue confirmation capture must keep the exact two-action, 0x10 effect, and six-status allowlists.'
}
if ([regex]::Matches($mchCapture, '\bConcurrentQueue<AllyRescueCleanseEffect>').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bMaximumQueuedAllyRescueCleanses\s*=\s*64\b').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bTryDequeueAllyRescueCleanse\s*\(').Count -ne 1) {
    throw 'Ally Rescue confirmation must use exactly one bounded 64-item queue and one dequeue boundary.'
}
if ([regex]::Matches($mchCapture, '\bConcurrentQueue<MiracleInterceptThreatEvent>').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bMaximumQueuedMiracleInterceptThreats\s*=\s*64\b').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bTryDequeueMiracleInterceptThreat\s*\(').Count -ne 1) {
    throw 'Miracle threat capture must use exactly one bounded 64-item queue and one public dequeue boundary.'
}
if ([regex]::Matches($mchCapture, '\bConcurrentQueue<MiracleInterceptLandedEffect>').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bMaximumQueuedMiracleInterceptConfirmations\s*=\s*64\b').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bTryDequeueMiracleInterceptConfirmation\s*\(').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bClearMiracleInterceptConfirmations\s*\(').Count -ne 3) {
    throw 'Miracle landing confirmation must use exactly one bounded 64-item queue, one dequeue boundary, and reviewed identity/reset clearing.'
}
$normalizedMiracleConfirmationCapture = $normalizedMchCapture
if ($normalizedMiracleConfirmationCapture -notmatch 'casterEntityId != localEntityId.*?header->NumTargets is 0 or > MaximumTargetsPerAction.*?var expectedStatus = MiracleInterceptConfirmationRules\.ExpectedStatusForAction\(actionId\); if \(expectedStatus == 0\) return null;.*?targetEntityId == localEntityId.*?for \(var slot = 0; slot < EffectSlotsPerTarget; slot\+\+\).*?effect\.Type != MiracleInterceptConfirmationRules\.AddStatusEffectType \|\| effect\.Value != expectedStatus.*?return new MiracleInterceptLandedEffect' -or
    $normalizedMiracleConfirmationCapture -notmatch 'confirmation\.CasterEntityId != CurrentMiracleInterceptLocalEntityId.*?confirmation\.FeatureGeneration != CurrentMiracleInterceptGeneration.*?!IsNetworkEntityId\(confirmation\.TargetEntityId\).*?if \(depth > MaximumQueuedMiracleInterceptConfirmations\).*?pendingMiracleInterceptConfirmations\.Enqueue\(confirmation\)') {
    throw 'Reactive-CC landing capture must require the exact local caster, a non-self network target, an action-specific WHM/BRD/NIN status, and AddStatus 0x0E before bounded enqueue.'
}
if ($normalizedMchCapture -notmatch 'MiracleInterceptRules\.ClassifyExactStartSignal\( actionId, casterEntityId, targetEntityId, header->NumTargets, targetEffects\[0\]\.Type, IsEmpty\(targetEffects\[0\]\), HasOnlyEmptyAdditionalEffects\(targetEffects\), header->AnimationVariation\)' -or
    $normalizedMchCapture -notmatch 'for \(var index = 1; index < effects\.Length; index\+\+\).*?!IsEmpty\(effects\[index\]\)') {
    throw 'Reactive-CC threat capture must pass exact single-target identity, all eight effect-slot facts, and animation variation to the pure classifier.'
}

if ($normalizedMchCapture -notmatch 'var localEntityId = actionId == EnemyCombatConstants\.PurifyActionId \? CurrentMiracleCleanseFollowupLocalEntityId : CurrentMiracleInterceptLocalEntityId; var featureGeneration = actionId == EnemyCombatConstants\.PurifyActionId \? CurrentMiracleCleanseFollowupGeneration : CurrentMiracleInterceptGeneration; if \(!IsNetworkEntityId\(localEntityId\) \|\| casterEntityId == localEntityId\) return null;' -or
    $normalizedMchCapture -notmatch 'public void SetMiracleCleanseFollowupLocalEntityId\(uint entityId\).*?ref miracleCleanseFollowupLocalEntityIdBits.*?if \(previous != normalized\) Interlocked\.Increment\(ref miracleCleanseFollowupGeneration\)') {
    throw 'Post-Purify CC capture must use its own opt-in local-identity gate and generation, independently from ordinary reactive-CC capture.'
}
if ($normalizedMchCapture -notmatch 'if \(actionId == EnemyCombatConstants\.PurifyActionId\).*?IsExactPurifySignal\( casterEntityId, actionId, targetEntityId, effectType: 0, effectValue: 0, header->GlobalSequence, header->SourceSequence\).*?ushort removedStatusId = 0; for \(var slot = 0; slot < EffectSlotsPerTarget; slot\+\+\).*?IsExactPurifySignal\( casterEntityId, actionId, targetEntityId, effect\.Type, effect\.Value, header->GlobalSequence, header->SourceSequence\).*?PurifyRemovalPriority\(effect\.Value\) > PurifyRemovalPriority\(removedStatusId\).*?new MiracleInterceptThreatEvent\( Environment\.TickCount64, localEntityId, casterEntityId, targetEntityId, actionId, header->AnimationVariation, removedStatusId == 0 \? \(byte\)0 : MiracleCleanseFollowupRules\.RecoveredFromStatusEffectType, removedStatusId, featureGeneration') {
    throw 'Post-Purify CC capture must accept the exact sequenced self-Purify action packet, optionally retain one deterministic reviewed recovery from its fixed eight slots, and leave live Resilience as the mandatory release authority.'
}
if ($normalizedMchCapture -notmatch 'var isCleanseFollowup = threat\.ActionId == EnemyCombatConstants\.PurifyActionId; var currentLocalEntityId = isCleanseFollowup \? CurrentMiracleCleanseFollowupLocalEntityId : CurrentMiracleInterceptLocalEntityId; var currentGeneration = isCleanseFollowup \? CurrentMiracleCleanseFollowupGeneration : CurrentMiracleInterceptGeneration;.*?threat\.FeatureGeneration != currentGeneration.*?if \(depth > MaximumQueuedMiracleInterceptThreats\).*?pendingMiracleInterceptThreats\.Enqueue\(threat\)') {
    throw 'The shared bounded Miracle queue must reject stale post-Purify feature generations before enqueueing; it may not add a second queue.'
}

$miracleInterceptRules = Read-RequiredSource (Join-Path $coreRoot 'MiracleInterceptRules.cs') 'Miracle intercept rules'
$normalizedMiracleInterceptRules = $miracleInterceptRules -replace '\s+', ' '
Assert-Literals $miracleInterceptRules @(
    'MarksmanSpiteActionId = 29_415',
    'ZantetsukenActionId = 29_537',
    'FuriousBacklashActionId = 39_188',
    'ContradanceActionId = 29_432',
    'HardenedScalesStatusId = 4_096',
    'MachinistJobId = 31',
    'SamuraiJobId = 34',
    'ViperJobId = 41',
    'DancerJobId = 38',
    'MarksmanSpiteThreatLifetimeMilliseconds = 500',
    'ZantetsukenThreatLifetimeMilliseconds = 500',
    'FuriousBacklashThreatLifetimeMilliseconds = 250',
    'ContradanceThreatLifetimeMilliseconds = 750',
    'MaximumObservedSignals = 128',
    'targetCount != 1',
    '!additionalEffectsAreCompletelyEmpty',
    'firstEffectType == 0x1B',
    'firstEffectIsCompletelyEmpty',
    'targetEntityId != casterEntityId',
    'targetEntityId == casterEntityId',
    'animationVariation == 0',
    'GetDispatchPriority',
    'PostGuardCrowdControl = 6',
    'MiracleInterceptThreatKind.PostPurifyCrowdControl or',
    'MiracleInterceptThreatKind.PostGuardCrowdControl => 1'
) 'Exact reactive-CC start-marker classifier and bounded one-shot policy'
if ($normalizedMiracleInterceptRules -notmatch 'MarksmanSpiteActionId when targetEntityId != casterEntityId && firstEffectType == 0x1B\s*=>\s*MiracleInterceptThreatKind\.MarksmanSpite' -or
    $normalizedMiracleInterceptRules -notmatch 'ZantetsukenActionId when targetEntityId != casterEntityId && firstEffectIsCompletelyEmpty\s*=>\s*MiracleInterceptThreatKind\.Zantetsuken' -or
    $normalizedMiracleInterceptRules -notmatch 'FuriousBacklashActionId when targetEntityId == casterEntityId && firstEffectIsCompletelyEmpty\s*=>\s*MiracleInterceptThreatKind\.FuriousBacklash' -or
    $normalizedMiracleInterceptRules -notmatch 'ContradanceActionId when targetEntityId == casterEntityId && animationVariation == 0 && firstEffectIsCompletelyEmpty\s*=>\s*MiracleInterceptThreatKind\.Contradance' -or
    $normalizedMiracleInterceptRules -notmatch 'MarksmanSpite or MiracleInterceptThreatKind\.Zantetsuken or MiracleInterceptThreatKind\.FuriousBacklash => 3, MiracleInterceptThreatKind\.Contradance => 2, MiracleInterceptThreatKind\.PostPurifyCrowdControl or MiracleInterceptThreatKind\.PostGuardCrowdControl => 1') {
    throw 'Pure reactive-CC classification must retain exact MCH 0x1B, SAM all-empty non-self, VPR all-empty self, DNC variation-0 all-empty self signatures, and urgent-before-DNC-before-both-follow-ups priority.'
}
if ($normalizedMiracleInterceptRules -notmatch 'MiracleInterceptThreatKind\.MarksmanSpite => jobId == MachinistJobId, MiracleInterceptThreatKind\.Zantetsuken => jobId == SamuraiJobId, MiracleInterceptThreatKind\.FuriousBacklash => jobId == ViperJobId, MiracleInterceptThreatKind\.Contradance => jobId == DancerJobId') {
    throw 'The four reviewed urgent startup signatures must remain bound to exact MCH, SAM, VPR, and DNC caster jobs before either WHM or BRD counter-CC can receive them.'
}

$miracleCleanseFollowupRules = Read-RequiredSource $miracleCleanseFollowupRulesPath 'Reactive CC post-Purify follow-up rules'
$normalizedMiracleCleanseFollowupRules = $miracleCleanseFollowupRules -replace '\s+', ' '
Assert-Literals $miracleCleanseFollowupRules @(
    'PurifyActionId = 29_056',
    'StunStatusId = 1_343',
    'HeavyStatusId = 1_344',
    'BindStatusId = 1_345',
    'SilenceStatusId = 1_347',
    'MiracleOfNatureStatusId = 3_085',
    'DeepFreezeStatusId = 3_219',
    'ResilienceStatusId = 3_248',
    'RecoveredFromStatusEffectType = 0x10',
    'ResilienceAcquisitionMilliseconds = 750',
    'ResilienceReleaseWaitMilliseconds = 3_000',
    'ResilienceMissingGraceMilliseconds = 150',
    'ReleaseOpportunityMilliseconds = 500',
    'MaximumResilienceRemainingMilliseconds = 2_250',
    'MaximumObservedSignals = 128',
    'MaximumPendingResolutions = 5',
    'MiracleCleanseFollowupSignalLedger',
    'RetiredSignals',
    'RetireValidatedSignal(',
    'IsNewValidatedSignal',
    'retired.Contains(signal)',
    'retired.Skip(skip).Append(signal).ToImmutableArray()',
    'MiracleCleanseFollowupPendingResolution',
    'MiracleCleanseFollowupResolutionObservation',
    'MiracleCleanseFollowupResolutionDecision',
    'ResolvePendingSignal(',
    'UniqueCanonicalTarget',
    'public bool ShouldRetry',
    'public bool DidResolve',
    'casterEntityId == targetEntityId',
    '(globalSequence != 0 || sourceSequence != 0)',
    'ActiveResilienceStatusCount',
    'ResiliencePresenceObserved',
    'ResilienceObservedAtMilliseconds',
    'ResilienceMissingSinceMilliseconds',
    'ResilienceRemainingMilliseconds',
    'ReservationGameplayKeyToken',
    'ReservedGameplayKeyPhysicallyDown',
    'CounterActionReachable',
    'GameplayKeyToken',
    'ExpectedProtectionEndAtMilliseconds',
    'ReservationKeyReleased = 15',
    'TeamTargetCountKnown',
    'TeamTargetCount',
    'HigherPriorityClaimed',
    'ReadyForPromotion',
    'PromotionIntent',
    'ReleasedAtMilliseconds >= Signal.ObservedAtMilliseconds &&',
    'GameplayKeyToken > 0',
    'state.ReleasedAtMilliseconds',
    'public bool ShouldPromote',
    'RetiresSignalBeforePromotion'
) 'Exact positive-observation reactive CC post-Purify policy'
if ($normalizedMiracleCleanseFollowupRules -notmatch 'IsExactPurifySignal\(.*?IsValidEntityId\(casterEntityId\) && casterEntityId == targetEntityId && actionId == PurifyActionId && \(\(effectType == 0 && effectValue == 0\) \|\| \(effectType == RecoveredFromStatusEffectType && IsPurifyRemovableStatus\(effectValue\)\)\) && \(globalSequence != 0 \|\| sourceSequence != 0\)' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'IsPurifyRemovableStatus\(uint statusId\) => statusId is StunStatusId or HeavyStatusId or BindStatusId or SilenceStatusId or MiracleOfNatureStatusId or DeepFreezeStatusId;' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'ValidateCandidate\(signal\.Target, observation\.Candidate\).*?value\.Target != expected \? MiracleCleanseFollowupCancelReason\.CandidateChanged') {
    throw 'The follow-up must remember one exact sequenced self-Purify action packet, optionally retain one of six reviewed recovered statuses, and bind it to one unchanged exact canonical actor.'
}
if ($normalizedMiracleCleanseFollowupRules -notmatch 'if \(age >= ResilienceAcquisitionMilliseconds\).*?ResilienceNotObserved.*?if \(candidate\.ActiveResilienceStatusCount == 0\).*?SignalObserved.*?Waiting.*?Phase = MiracleCleanseFollowupPhase\.WaitingForResilienceEnd, ResiliencePresenceObserved = true, ResilienceObservedAtMilliseconds = nowMilliseconds' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'if \(!state\.ResiliencePresenceObserved \|\| state\.ResilienceObservedAtMilliseconds < 0\).*?if \(candidate\.ActiveResilienceStatusCount == 1\).*?ResilienceMissingSinceMilliseconds = -1.*?ExpectedProtectionEndAtMilliseconds = UpdateExpectedProtectionEnd\(.*?if \(state\.ExpectedProtectionEndAtMilliseconds > 0 && observation\.NowMilliseconds >= state\.ExpectedProtectionEndAtMilliseconds\).*?Phase = MiracleCleanseFollowupPhase\.ReleaseOpportunity.*?ObserveReleaseOpportunity\(predictedRelease, candidate, observation\).*?if \(state\.ResilienceMissingSinceMilliseconds < 0\).*?ResilienceMissingSinceMilliseconds = observation\.NowMilliseconds.*?if \(missingAge < ResilienceMissingGraceMilliseconds\) return Waiting\(state\);.*?Phase = MiracleCleanseFollowupPhase\.ReleaseOpportunity') {
    throw 'Resilience must be positively observed within 750 ms; live presence stays authoritative, while only an actual absent sample at a bounded expected end may skip the 150-ms anti-flicker grace.'
}
if ($normalizedMiracleCleanseFollowupRules -notmatch 'var age = observation\.NowMilliseconds - state\.ResilienceObservedAtMilliseconds; if \(age < 0\).*?ClockMovedBackwards.*?if \(age >= ResilienceReleaseWaitMilliseconds\).*?ResilienceReleaseTimedOut.*?if \(candidate\.ActiveResilienceStatusCount == 1\)' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'var releaseLifetime = state\.GameplayKeyToken > 0 \? MiracleProtectionEndRules\.HeldLeaseMilliseconds : ReleaseOpportunityMilliseconds; if \(releaseAge >= releaseLifetime\).*?ReleaseOpportunityExpired.*?if \(observation\.HigherPriorityClaimed\) return Waiting\(state\);.*?new MiracleCleanseFollowupIntent\( signal, state\.ReleasedAtMilliseconds\).*?StopTracking\(state, observation\.NowMilliseconds\).*?ReadyForPromotion.*?intent') {
    throw 'Post-Purify must retain its hard Resilience deadline and actual absence, then allow only a 500-ms consent-acquisition edge followed by the shared three-second bound actor/key lease measured from release.'
}
if ($normalizedMiracleCleanseFollowupRules -notmatch 'var releaseAge = observation\.NowMilliseconds - state\.ReleasedAtMilliseconds;.*?var releaseLifetime = state\.GameplayKeyToken > 0 \? MiracleProtectionEndRules\.HeldLeaseMilliseconds : ReleaseOpportunityMilliseconds;.*?if \(releaseAge >= releaseLifetime\).*?if \(state\.GameplayKeyToken > 0\).*?if \(!candidate\.ReservedGameplayKeyPhysicallyDown\).*?MiracleCleanseFollowupCancelReason\.ReservationKeyReleased.*?else if \(candidate\.ReservationGameplayKeyToken > 0 && candidate\.ReservedGameplayKeyPhysicallyDown\).*?GameplayKeyToken = candidate\.ReservationGameplayKeyToken.*?if \(observation\.HigherPriorityClaimed\) return Waiting\(state\);.*?if \(state\.GameplayKeyToken <= 0\) return Waiting\(state\);' -or
    [regex]::Matches($miracleCleanseFollowupRules, 'GameplayKeyToken\s*=\s*candidate\.ReservationGameplayKeyToken').Count -ne 1 -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'private static long UpdateExpectedProtectionEnd\(.*?remainingMilliseconds <= 0 \|\| remainingMilliseconds > MaximumResilienceRemainingMilliseconds \|\| nowMilliseconds < 0.*?return currentExpectedEndMilliseconds;.*?nowMilliseconds > long\.MaxValue - remainingMilliseconds \? long\.MaxValue : nowMilliseconds \+ remainingMilliseconds; return currentExpectedEndMilliseconds > 0 \? Math\.Min\(currentExpectedEndMilliseconds, observedEnd\) : observedEnd;') {
    throw 'Post-Purify must remember the actor episode before consent, acquire one current exact eligible key only inside the 500-ms edge, and freeze that actor/key through the remaining three-second dispatcher lease.'
}
if ($miracleCleanseFollowupRules -match '\b(HasExactTeamFocus|RequiredTeamTargetCount)\b' -or
    $normalizedMiracleCleanseFollowupRules -match 'TeamTargetCount\s*>=\s*[12]\b') {
    throw 'Post-Purify must have no minimum team-pressure gate; unknown pressure remains eligible and known zero remains valid ranking data.'
}
if ($normalizedMiracleCleanseFollowupRules -notmatch 'RetireValidatedSignal\( MiracleCleanseFollowupSignalLedger previous, MiracleCleanseFollowupSignalKey signal\).*?IsExactPurifySignal\(.*?\|\| retired\.Contains\(signal\).*?IsNewValidatedSignal: false.*?var skip = Math\.Max\(0, retired\.Length - MaximumObservedSignals \+ 1\);.*?retired\.Skip\(skip\)\.Append\(signal\)\.ToImmutableArray\(\).*?IsNewValidatedSignal: true' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'MiracleCleanseFollowupPendingResolution\( MiracleCleanseFollowupSignalKey Key, long ObservedAtMilliseconds, uint LocalEntityId, uint LocalCounterJobId, int FeatureGeneration\).*?public bool IsValid => MiracleCleanseFollowupRules\.IsExactPurifySignal\( Key\.CasterEntityId, Key\.ActionId, Key\.TargetEntityId, Key\.EffectType, Key\.EffectValue, Key\.GlobalSequence, Key\.SourceSequence\) && ObservedAtMilliseconds >= 0 && MiracleCleanseFollowupRules\.IsValidEntityId\(LocalEntityId\) && LocalEntityId != Key\.CasterEntityId && LocalCounterJobId != 0;' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'ResolvePendingSignal\( MiracleCleanseFollowupPendingResolution pending, MiracleCleanseFollowupResolutionObservation observation\).*?if \(observation\.HardReset\).*?if \(!pending\.IsValid\).*?if \(!observation\.ConfigurationEnabled\).*?if \(!observation\.IsCrystallineConflict\).*?if \(!observation\.IsLocalCounterJobValid \|\| observation\.LocalCounterJobId == 0\).*?if \(observation\.LocalEntityId != pending\.LocalEntityId\).*?if \(observation\.LocalCounterJobId != pending\.LocalCounterJobId\).*?if \(observation\.FeatureGeneration != pending\.FeatureGeneration\).*?if \(observation\.NowMilliseconds < pending\.ObservedAtMilliseconds\).*?observation\.NowMilliseconds - pending\.ObservedAtMilliseconds >= ResilienceAcquisitionMilliseconds.*?MiracleCleanseFollowupResolutionRetireReason\.AcquisitionExpired' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'if \(observation\.UniqueCanonicalTarget is not \{ \} target\).*?MiracleCleanseFollowupResolutionDecisionKind\.Waiting, pending, null, MiracleCleanseFollowupResolutionRetireReason\.None.*?if \(!target\.IsValid \|\| target\.EntityId != pending\.Key\.CasterEntityId \|\| target\.EntityId != pending\.Key\.TargetEntityId\).*?MiracleCleanseFollowupResolutionRetireReason\.CanonicalIdentityChanged.*?MiracleCleanseFollowupResolutionDecisionKind\.Resolved, null, new MiracleCleanseFollowupSignal\( pending\.Key, target, pending\.ObservedAtMilliseconds\)') {
    throw 'Every already-validated Purify packet must enter one bounded terminal ledger before canonical lookup. Only that immutable signal may retry exact canonical resolution inside its original 750-ms acquisition deadline; duplicates, gate drift, deadline expiry, or changed identity are terminal.'
}
if ($miracleCleanseFollowupRules -match '\b(UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event|StatusAddress|StatusInstanceToken|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b|\bstatus\.[A-Za-z_]*Address\b') {
    throw 'Pure post-Purify rules must never dispatch, mutate targets/input, inspect native status storage, or own a runtime clock/timer.'
}

$miracleGuardFollowupRules = Read-RequiredSource $miracleGuardFollowupRulesPath 'Reactive CC post-Guard follow-up rules'
$normalizedMiracleGuardFollowupRules = $miracleGuardFollowupRules -replace '\s+', ' '
Assert-Literals $miracleGuardFollowupRules @(
    'GuardStatusId = 3_054',
    'GuardStatusAlternateId = 3_673',
    'ReleaseOpportunityMilliseconds = 500',
    'MaximumGuardRemainingMilliseconds = 4_250',
    'WaitingForGuard',
    'GuardPresent',
    'ReleaseOpportunity',
    'RetiredUntilGuardAbsent',
    'EnemySlotRules.IsValidSlot(EnemySlot)',
    'MiracleGuardFollowupRules.IsValidEntityId(EntityId)',
    'ActiveGuardStatusCount is 0 or 1',
    'TeamTargetCountKnown',
    '(!TeamTargetCountKnown || TeamTargetCount >= 0)',
    'HasTrustedMp',
    'GuardRemainingMilliseconds',
    'ReservationGameplayKeyToken',
    'ReservedGameplayKeyPhysicallyDown',
    'CounterActionReachable',
    'GameplayKeyToken',
    'ExpectedProtectionEndAtMilliseconds',
    'MaximumMp == CombatFrameRules.ExpectedMaximumMp',
    'RankCandidate',
    'previous.Phase == MiracleGuardFollowupPhase.GuardPresent',
    'ReleasedAtMilliseconds = nowMilliseconds',
    'nowMilliseconds - actor.ReleasedAtMilliseconds < ReleaseOpportunityMilliseconds',
    'observation.HigherPriorityClaimed',
    'ProtectionEndRankComparer.Instance',
    'MiracleProtectionEndRules.Compare(left.RankCandidate, right.RankCandidate)',
    'Math.Max(0, releaseReady.Length - 1)',
    'actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity',
    'MiracleGuardFollowupActorState.Waiting(actor.Target)',
    'public bool ShouldPromote'
) 'Exact positive-presence first-absence post-Guard one-shot policy'
if ($normalizedMiracleGuardFollowupRules -notmatch 'if \(guardPresent\).*?var firstPresence = previous\.Phase != MiracleGuardFollowupPhase\.GuardPresent;.*?MiracleGuardFollowupPhase\.GuardPresent, firstPresence \? nowMilliseconds : previous\.GuardObservedAtMilliseconds.*?GameplayKeyToken = 0.*?UpdateExpectedProtectionEnd\( firstPresence \? -1 : previous\.ExpectedProtectionEndAtMilliseconds, candidate\.GuardRemainingMilliseconds, nowMilliseconds\).*?if \(previous\.Phase == MiracleGuardFollowupPhase\.GuardPresent\).*?Phase = MiracleGuardFollowupPhase\.ReleaseOpportunity, ReleasedAtMilliseconds = nowMilliseconds, GameplayKeyToken = candidate\.ReservationGameplayKeyToken > 0 && candidate\.ReservedGameplayKeyPhysicallyDown \? candidate\.ReservationGameplayKeyToken : 0' -or
    $normalizedMiracleGuardFollowupRules -notmatch 'if \(previous\.Phase == MiracleGuardFollowupPhase\.ReleaseOpportunity && previous\.GameplayKeyToken > 0 && !candidate\.ReservedGameplayKeyPhysicallyDown\).*?MiracleGuardFollowupActorState\.Waiting\(candidate\.Target\).*?if \(IsInsideReleaseOrHeldWindow\(previous, nowMilliseconds\)\).*?previous\.GameplayKeyToken == 0 && candidate\.ReservationGameplayKeyToken > 0 && candidate\.ReservedGameplayKeyPhysicallyDown \? previous with \{ GameplayKeyToken = candidate\.ReservationGameplayKeyToken, \} : previous' -or
    $normalizedMiracleGuardFollowupRules -notmatch 'releaseReady.*?IsInsideReleaseOrHeldWindow\( pair\.Actor, observation\.NowMilliseconds\).*?var previouslyFrozen = previous\.Actors.*?GameplayKeyToken > 0.*?var selected = hadPreviouslyFrozen \? releaseReady\.FirstOrDefault.*?: releaseReady \.Where\(static pair => pair\.Actor\.GameplayKeyToken > 0\) \.OrderBy\(static pair => pair\.Candidate, ProtectionEndRankComparer\.Instance\) \.FirstOrDefault\(\).*?var retiredOtherOpportunities = Math\.Max\(0, releaseReady\.Length - 1\); var frozenActors = state\.Actors.*?actor\.Target != selected\.Actor\.Target \? MiracleGuardFollowupActorState\.Waiting\(actor\.Target\).*?var frozenState = new MiracleGuardFollowupState\( frozenActors, observation\.NowMilliseconds\); if \(observation\.HigherPriorityClaimed\).*?Waiting\( frozenState,.*?retiredOtherOpportunities\)' -or
    $normalizedMiracleGuardFollowupRules -notmatch 'var retiredActors = frozenState\.Actors.*?actor\.Phase == MiracleGuardFollowupPhase\.ReleaseOpportunity \? MiracleGuardFollowupActorState\.Waiting\(actor\.Target\).*?ReadyForPromotion') {
    throw 'Post-Guard must remember keyless actor episodes, acquire consent only inside the strict 500-ms edge, freeze one ranked actor/key, retire every simultaneous loser before a priority wait, and retain only that winner through the three-second deadline measured from release.'
}
if ($normalizedMiracleGuardFollowupRules -notmatch 'public bool IsValid => Target\.IsValid && ReleasedAtMilliseconds >= 0 && GameplayKeyToken > 0' -or
    $normalizedMiracleGuardFollowupRules -notmatch 'private static bool IsInsideReleaseOrHeldWindow\(.*?actor\.GameplayKeyToken > 0 \? MiracleProtectionEndRules\.IsInsideHeldLease\( actor\.ReleasedAtMilliseconds, nowMilliseconds\) : IsInsideReleaseWindow\(actor, nowMilliseconds\);') {
    throw 'A Guard release without an exact key remains acquisition-only for 500 ms; once bound, its chosen actor/key may wait only until three seconds from the original release.'
}
if ($normalizedMiracleGuardFollowupRules -notmatch 'if \(previous\.Phase == MiracleGuardFollowupPhase\.RetiredUntilGuardAbsent\).*?guardPresent \? previous : MiracleGuardFollowupActorState\.Waiting\(candidate\.Target\)' -or
    $normalizedMiracleGuardFollowupRules -notmatch 'previousBySlot\.Values.*?actor\.Phase is MiracleGuardFollowupPhase\.GuardPresent or MiracleGuardFollowupPhase\.RetiredUntilGuardAbsent.*?!candidates\.ContainsKey\(actor\.Target\.EnemySlot\).*?ShouldRetainUncertainActor.*?retained\.Phase == MiracleGuardFollowupPhase\.GuardPresent \? retained with.*?Phase = MiracleGuardFollowupPhase\.RetiredUntilGuardAbsent.*?GameplayKeyToken = 0.*?: retained' -or
    $normalizedMiracleGuardFollowupRules -notmatch 'private static bool ShouldRetainUncertainActor\(.*?if \(observedCandidates is null \|\| observedCandidates\.Count == 0\) return true;.*?if \(sameSlot\.Length == 0\) return true;.*?if \(sameIdentity\.Length == 0\) return false;.*?if \(sameIdentity\.Length != 1 \|\| !sameIdentity\[0\]\.IsExactCanonicalEnemy\).*?return true;.*?return sameIdentity\[0\]\.IsAliveAndTargetable && sameIdentity\[0\]\.CurrentHp > 0;') {
    throw 'An ambiguous Guard-presence episode must remain a keyless tombstone through unknown samples; only proven identity/life loss drops it, and exact Guard absence separates a later episode. After release-time key binding, key release is terminal for that opportunity.'
}
if ($normalizedMiracleGuardFollowupRules -notmatch 'private static long UpdateExpectedProtectionEnd\(.*?remainingMilliseconds <= 0 \|\| remainingMilliseconds > MaximumGuardRemainingMilliseconds \|\| nowMilliseconds < 0.*?return currentExpectedEndMilliseconds;.*?return currentExpectedEndMilliseconds > 0 \? Math\.Min\(currentExpectedEndMilliseconds, observedEnd\) : observedEnd;' -or
    $normalizedMiracleGuardFollowupRules -match 'if \([^)]*ExpectedProtectionEndAtMilliseconds') {
    throw 'Guard RemainingTime must remain only a bounded, non-extending diagnostic/wake-up hint and may never delay or authorize the first real absence edge.'
}
if ($miracleGuardFollowupRules -match '\b(HasExactTeamFocus|RequiredTeamTargetCount)\b' -or
    $normalizedMiracleGuardFollowupRules -match 'TeamTargetCount\s*>=\s*[12]\b' -or
    $miracleGuardFollowupRules -match '\b(ResilienceMissingGraceMilliseconds|MissingGrace|150)\b' -or
    $miracleGuardFollowupRules -match '\b(UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event|StatusAddress|StatusInstanceToken|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b|\bstatus\.[A-Za-z_]*Address\b') {
    throw 'Pure post-Guard rules must have no pressure minimum or 150-ms absence grace and must never dispatch, mutate targets/input, inspect native status storage, or own a runtime clock/timer.'
}

$miracleProtectionEndRules = Read-RequiredSource $miracleProtectionEndRulesPath 'Shared protection-end consent and ranking rules'
$normalizedMiracleProtectionEndRules = $miracleProtectionEndRules -replace '\s+', ' '
Assert-Literals $miracleProtectionEndRules @(
    'HeldLeaseMilliseconds = 3_000',
    'NinjaWeaponskillHeldLeaseMilliseconds = 3_000',
    'MiracleProtectionEndHeldConsentState(int GameplayKeyToken)',
    'UnconsumedEligibleGameplayKeyToken',
    'LatchedKeyPhysicallyDown',
    'observation.HardReset',
    '!observation.Enabled',
    'observation.IsTextInputActive',
    'previous.IsLatched && observation.LatchedKeyPhysicallyDown',
    'observation.UnconsumedEligibleGameplayKeyToken > 0',
    'DispatchConsumesHeldConsent',
    'MiracleInterceptThreatKind.PostPurifyCrowdControl',
    'MiracleInterceptThreatKind.PostGuardCrowdControl',
    'CanPreemptUnattemptedLowerPriorityThreat(',
    'activeRetryState == HeldActionRetryState.Initial',
    'var activePriority = MiracleInterceptRules.GetDispatchPriority(activeThreat)',
    'var incomingPriority = MiracleInterceptRules.GetDispatchPriority(incomingThreat)',
    'incomingPriority > activePriority',
    'TeamTargetCountKnown',
    '(!TeamTargetCountKnown || TeamTargetCount >= 0)',
    'CombatFrameRules.ExpectedMaximumMp',
    'UInt128',
    'SelectBestIndex'
) 'Shared protection-end exact held consent and ranking policy'
if ($normalizedMiracleProtectionEndRules -notmatch 'one ordinary 2\.5-second GCD plus the.*?release edge.*?HeldLeaseMilliseconds = 3_000;.*?NinjaWeaponskillHeldLeaseMilliseconds = 3_000;') {
    throw 'Every protection-end counter must retain the shared exact 3,000-ms one-GCD lease.'
}
if ($normalizedMiracleProtectionEndRules -notmatch 'DispatchConsumesHeldConsent\(MiracleInterceptThreatKind threat\) => false;' -or
    $normalizedMiracleProtectionEndRules -notmatch 'if \(observation\.HardReset \|\| !observation\.Enabled \|\| observation\.IsTextInputActive\).*?MiracleProtectionEndHeldConsentState\.Initial.*?if \(previous\.IsLatched && observation\.LatchedKeyPhysicallyDown\) return previous;.*?observation\.UnconsumedEligibleGameplayKeyToken > 0.*?MiracleProtectionEndHeldConsentState\.Initial') {
    throw 'No dispatch may globally consume continuous held consent; exact consent persists only while physically held and clears on release/text/disable/reset.'
}
if ($normalizedMiracleProtectionEndRules -notmatch 'CanPreemptUnattemptedLowerPriorityThreat\( MiracleInterceptThreatKind activeThreat, HeldActionRetryState activeRetryState, MiracleInterceptThreatKind incomingThreat\).*?var activePriority = MiracleInterceptRules\.GetDispatchPriority\(activeThreat\); var incomingPriority = MiracleInterceptRules\.GetDispatchPriority\(incomingThreat\); return activeRetryState == HeldActionRetryState\.Initial && activePriority > 0 && incomingPriority > activePriority;') {
    throw 'A new exact urgent event may replace only an unattempted strictly lower-priority reactive lease; native-attempt ownership and equal/higher priority remain frozen.'
}
if ($normalizedMiracleProtectionEndRules -notmatch 'leftHasPositivePressure = left\.TeamTargetCountKnown && left\.TeamTargetCount > 0;.*?rightHasPositivePressure = right\.TeamTargetCountKnown && right\.TeamTargetCount > 0;.*?positivePressure = rightHasPositivePressure\.CompareTo\(leftHasPositivePressure\);.*?if \(leftHasPositivePressure\).*?right\.TeamTargetCount\.CompareTo\(left\.TeamTargetCount\).*?hpRatio = CompareRatio\( left\.CurrentHp, left\.MaximumHp, right\.CurrentHp, right\.MaximumHp\).*?mpTrust = right\.HasTrustedMp\.CompareTo\(left\.HasTrustedMp\).*?if \(left\.HasTrustedMp\).*?mpRatio = CompareRatio\( left\.CurrentMp, left\.MaximumMp, right\.CurrentMp, right\.MaximumMp\).*?left\.EnemySlot\.CompareTo\(right\.EnemySlot\).*?left\.EntityId\.CompareTo\(right\.EntityId\).*?left\.GameObjectId\.CompareTo\(right\.GameObjectId\).*?left\.JobId\.CompareTo\(right\.JobId\).*?left\.Threat\.CompareTo\(right\.Threat\)' -or
    $normalizedMiracleProtectionEndRules -notmatch 'for \(var index = 0; index < candidates\.Count; index\+\+\).*?if \(!candidates\[index\]\.IsValid\) continue;.*?Compare\(candidates\[index\], candidates\[selected\]\) < 0.*?return selected') {
    throw 'Protection-end ranking must choose one winner by optional fresh positive pressure bonus, then ascending exact HP, known trusted exact-10k MP before unknown/ascending, and stable S-slot/IDs; zero and unknown/stale pressure are neutral peers.'
}
if ($miracleProtectionEndRules -match '\b(HasExactTeamFocus|RequiredTeamTargetCount)\b|TeamTargetCount\s*>=\s*[12]\b') {
    throw 'Shared protection-end ranking must not restore a minimum pressure threshold or fabricate unknown pressure as zero.'
}

$miracleCleanseFollowupSelfTests = Read-RequiredSource $miracleCleanseFollowupSelfTestsPath 'Reactive CC post-Purify self-tests'
$miracleGuardFollowupSelfTests = Read-RequiredSource $miracleGuardFollowupSelfTestsPath 'Reactive CC post-Guard self-tests'
$miracleProtectionEndSelfTests = Read-RequiredSource $miracleProtectionEndSelfTestsPath 'Shared protection-end self-tests'
$miracleGuardProgram = Read-RequiredSource (Join-Path $coreSelfTestRoot 'Program.cs') 'Core self-test registry'
$miracleCleanseTestMethods = @(
    'ExactPurifySignalAcceptsActionLevelOrKnownRecovery',
    'ValidatedSignalRetriesOnlyCanonicalResolutionInsideOriginalDeadline',
    'ExactLifecyclePromotesOnceAfterObservedRelease',
    'MissingGraceRejectsFlickerAndAmbiguity',
    'AcquisitionReleaseAndOpportunityWindowsAreBounded',
    'HigherPriorityWaitsWithoutDestroyingOpportunity',
    'TeamPressureHasNoMinimumAndUnknownRemainsEligible',
    'IdentityAmbiguityAndConcurrencyFailClosed',
    'IndependentEnemySlotsKeepDistinctPurifyEpisodes',
    'ExpectedEndUsesFirstAuthoritativeAbsentFrame',
    'InvalidExpectedEndKeepsAbsenceGrace',
    'ReservationBindsAtReleaseAndThenRequiresExactKey',
    'PromotionKindLabelsConfirmationWithoutBroadeningStartRules'
)
foreach ($method in $miracleCleanseTestMethods) {
    Assert-Literals $miracleCleanseFollowupSelfTests @("internal static void $method()") "Post-Purify self-test $method"
    Assert-Literals $miracleGuardProgram @("MiracleCleanseFollowupSelfTests.$method") "Post-Purify test registration $method"
}
$miracleGuardTestMethods = @(
    'ExactGuardRowsAndAbsenceCannotSyntheticArm',
    'FirstVerifiedAbsentFramePromotesOnceAndRequiresPositiveRearm',
    'PressureHasNoMinimumAndPriorityWaitsInsideOriginalWindow',
    'SimultaneousReleaseUsesPositivePressureBonusThenFallbacks',
    'IdentityLifeAndStatusAmbiguityBreakTheEpisode',
    'ConfigurationContextClockAndHardResetClearAllEpisodes',
    'ReservationBindsOnGuardEndAndAllowsEarlyCancel',
    'ReleaseOpportunityAcquiresOnceAndThenRequiresExactKey'
)
foreach ($method in $miracleGuardTestMethods) {
    Assert-Literals $miracleGuardFollowupSelfTests @("internal static void $method()") "Post-Guard self-test $method"
    Assert-Literals $miracleGuardProgram @("MiracleGuardFollowupSelfTests.$method") "Post-Guard test registration $method"
}
$miracleProtectionEndTestMethods = @(
    'HeldConsentRequiresOneExactUnconsumedGeneration',
    'RankingUsesPositivePressureBonusThenHealthMpAndIdentity',
    'WhiteMageBardAndNinjaShareProtectionEndSemantics',
    'HeldLeaseSurvivesPriorityAndRetriesOnlyInsideItsBound'
)
foreach ($method in $miracleProtectionEndTestMethods) {
    Assert-Literals $miracleProtectionEndSelfTests @("internal static void $method()") "Protection-end self-test $method"
    Assert-Literals $miracleGuardProgram @("MiracleProtectionEndSelfTests.$method") "Protection-end test registration $method"
}
if ([regex]::Matches($miracleCleanseFollowupSelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 13 -or
    [regex]::Matches($miracleGuardProgram, '\bMiracleCleanseFollowupSelfTests\.\w+').Count -ne 13 -or
    [regex]::Matches($miracleGuardFollowupSelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 8 -or
    [regex]::Matches($miracleGuardProgram, '\bMiracleGuardFollowupSelfTests\.\w+').Count -ne 8) {
    throw 'All thirteen post-Purify and eight post-Guard lifecycle tests must remain registered exactly once.'
}
if ([regex]::Matches($miracleProtectionEndSelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 4 -or
    [regex]::Matches($miracleGuardProgram, '\bMiracleProtectionEndSelfTests\.\w+').Count -ne 4 -or
    [regex]::Matches($miracleGuardProgram, '(?m)^\s*\("').Count -ne 388) {
    throw 'All four shared protection-end tests and the exact 388-test Core registry must remain pinned.'
}
Assert-Literals $miracleCleanseFollowupSelfTests @(
    'first validated packet is terminally remembered',
    'one transient canonical miss retains only the same exact signal',
    'canonical retry cannot replace or mutate signal identity',
    'missing canonical identity cannot arm the lifecycle',
    'duplicate packet cannot enqueue or extend resolution',
    'duplicate adds no second retirement',
    'the same signal may resolve at 749ms',
    'resolution is removed before lifecycle exposure',
    'resolution preserves the original packet and timestamp',
    'the original exact 750ms acquisition deadline is terminal',
    'deadline retirement cannot retry',
    'a different canonical actor is terminal rather than a fallback',
    'disable/context/death-job-generation/reset gates clear pending state',
    'closed gate retains no pending resolution',
    'pending storage is bounded to the five canonical enemy slots',
    'unknown pressure is an eligible lower-rank fallback',
    'fresh known pressure zero has no minimum gate',
    'first exact slot keeps its own release episode',
    'second exact slot keeps its own release episode',
    'first distinct slot can yield one candidate',
    'second distinct slot can yield one candidate',
    'validated expected end is absolute',
    'expected time alone never authorizes through live Resilience',
    'enemy episode stores no early key during immunity',
    'movement-key changes remain irrelevant during immunity',
    'first executable frame freezes the current exact key',
    'first live absence after expected end promotes immediately',
    'actual absence remains release authority',
    'implausible duration is not trusted',
    'untimed first absence retains anti-flicker grace',
    'an exact out-of-range release freezes now and waits in the bounded dispatcher lease',
    'promotion retires the short release window before the dispatcher waits for native reachability',
    'releasing or changing a movement key during Resilience does not destroy the enemy episode',
    'release is remembered even when no key exists on its first frame',
    'a current held key can be acquired inside the original release window',
    'once release begins, the exact key is frozen while priority work runs',
    'after release binding, letting go terminally cancels the exact intent'
) 'Focused post-Purify rank, action-level sentinel, release-time key binding, reachability, advisory timing, and bounded episode coverage'
Assert-Literals $miracleGuardFollowupSelfTests @(
    'absence without prior exact presence cannot arm',
    'first verified absent framework frame promotes immediately',
    'absence cannot retry a spent episode',
    'a later positive Guard rearms',
    'priority clearing after 500 ms still promotes the original bound lease',
    'priority wait cannot restart the three-second held lease',
    'fresh known pressure zero may promote at 499 ms',
    'unbound 500 ms acquisition boundary is already expired',
    'highest fresh exact pressure wins before HP',
    'zero and unavailable pressure both remain eligible',
    'known zero and unavailable pressure are neutral, so lower HP wins',
    'lower trusted MP ratio wins before S-slot',
    'equal HP ratio uses lower S-slot',
    'other ready opportunity is spent',
    'simultaneous loser retires before the priority wait',
    'loser cannot remain available for later reranking',
    'the selected held lease remains promotable after the 500 ms acquisition edge',
    'later rank changes cannot replace the frozen actor',
    'later promotion keeps the exact frozen key',
    'held lease remains measured from authoritative Guard end',
    'retired opportunity cannot dispatch on a later frame',
    'same slot with changed life identity cannot release',
    'duplicate Guard rows retire without becoming absence proof or allowing rearm',
    'shared landing confirmation accepts the post-Guard label without a removed status',
    'first Guard frame owns the episode epoch',
    'Guard presence stores the enemy episode without binding an early key',
    'Guard duration becomes an advisory absolute end',
    'continued Guard cannot restart the epoch',
    'movement-key changes during Guard do not own or cancel the episode',
    'later telemetry cannot extend the hint',
    'manual Guard cancel releases on its first authoritative absent frame',
    'early release freezes the currently held exact key',
    'timer never delays early Guard cancel',
    'key release during Guard does not retire the enemy episode',
    'Guard end without a key opens but cannot dispatch the release opportunity',
    'a current key may bind inside the original release window',
    'once Guard has ended, the exact key is frozen through priority waits',
    'releasing the frozen key cancels this exact opportunity',
    'one exact release freezes immediately even while range is transient',
    'ranking freezes the strongest exact release before range/line-of-sight dispatcher wait'
) 'Focused post-Guard actor-first memory, release-time key binding, rank-before-reachability, advisory timing, early release, identity, expiry, and confirmation coverage'
Assert-Literals $miracleProtectionEndSelfTests @(
    'raw physical level cannot invent consent',
    'one shared unconsumed eligible token acquires consent',
    'the exact key token is frozen',
    'shared consumption does not erase a proven hold',
    'physical release clears exact consent',
    'startup reactive dispatch keeps the continuous hold available',
    'all reactive families leave the same hold available for later actions',
    'post-Purify dispatch retains consent for a later distinct episode',
    'post-Guard dispatch retains consent for a later distinct episode',
    'exact higher-priority {incoming} preempts unattempted {active}',
    'a native attempt freezes the protection-end lease against preemption',
    'a strictly higher urgent startup preempts an unattempted lower urgent lease',
    'equal/lower protection-end priority cannot preempt',
    'a cast or higher-priority accepted action may finish before exact counter-CC dispatch',
    'every protection-end counter keeps one ordinary GCD plus its release allowance',
    'retry cannot run before the shared throttle',
    'NIN keeps one verified 2.5-second weaponskill recast plus the 500 ms release allowance',
    'NIN remains eligible immediately before its exact 3000 ms lease ends',
    'NIN lease has an exclusive 3000 ms boundary',
    'known zero and unknown pressure are neutral, so HP wins',
    'zero cannot outrank unknown before stable identity',
    'higher positive pressure ranks before HP',
    'any positive fresh pressure earns the optional bonus',
    'lower exact HP ratio ranks next',
    'trusted MP ranks ahead of unknown MP',
    'lower trusted MP ratio ranks next',
    'one deterministic winner is selected'
) 'Shared protection-end held-consent, startup ownership, NIN 3000-ms lease, later-epoch reuse, telemetry trust, and single-winner coverage'

$mchMarkerRules = Read-RequiredSource (Join-Path $coreRoot 'MachinistLimitBreakMarkerRules.cs') 'MCH LB marker rules'
Assert-Literals $mchMarkerRules @(
    'MarksmanSpiteActionId = 29_415',
    'TargetMarkerEffectType = 0x1B',
    'MaximumTargets = 32',
    '!hasAdditionalEffects'
) 'Exact MCH LB early-marker rules'

$purifyProbe = Read-RequiredSource $purifyProbePath 'Emergency Purify probe'
$useActionCalls = [regex]::Matches($purifyProbe, '\bUseAction\s*\(')
if ($useActionCalls.Count -ne 1) {
    throw "Emergency Purify probe must contain exactly one UseAction call; found $($useActionCalls.Count)."
}
$normalizedPurifyProbe = $purifyProbe -replace '\s+', ' '
if ($normalizedPurifyProbe -notmatch 'UseAction\s*\(\s*ActionType\.Action\s*,\s*EnemyCombatConstants\.PurifyActionId\s*,\s*localPlayer\.GameObjectId\s*,[\s\S]*?UseActionMode\.None[\s\S]*?\)') {
    throw 'Purify must use ActionType.Action, the verified Purify constant, the local player GameObjectId, and UseActionMode.None.'
}
Assert-Literals $purifyProbe @(
    'EmergencyPurifyBufferRules.Observe',
    'EmergencyPurifyBufferRules.ClaimsSchedulerPriority(',
    'EmergencyPurifyBufferRules.ApplyNativeAttemptOutcome(',
    'ActionManager.Instance',
    'configurationEnabled',
    'localPlayerIdentityValid',
    'statusCurrentlyObserved',
    'resilienceActive',
    'allowHeldKeyAtStatusEntry',
    'decision.ShouldClaimInputFrame',
    'inputFrame.Consume()',
    'ClientActionAttemptBoundary.IsExactActionReady(',
    'HeldActionRetryRules.IsNativeBoundaryNearQueueable(',
    'ClientActionAttemptBoundary.Capture(',
    'ClientActionAttemptBoundaryRules.Classify(',
    'Purify waiting without spending retry budget',
    'Purify client-rejected; exact intent retained for bounded retry',
    'InputClaimed = inputClaimed'
) 'Emergency Purify shared retry boundary and absolute scheduler claim'
if ([regex]::Matches($purifyProbe, '\bstatusCurrentlyObserved\b').Count -lt 3 -or
    [regex]::Matches($purifyProbe, '\bClientActionAttemptBoundary\.Capture\s*\(').Count -ne 3 -or
    [regex]::Matches($purifyProbe, '\bClientActionAttemptBoundaryRules\.Classify\s*\(').Count -ne 1 -or
    $purifyProbe -match '\b(IGameInteropProvider|Hook<|HookFromAddress|SignatureAttribute|SigScanner|ITargetManager|TargetManager|SetTarget|QueueAction|AlternateAction|AlternateTarget)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Emergency Purify must freeze the exact current CC/self intent, classify one direct self-GOID action boundary, capture one separate cast-cancel readiness boundary, and never hook, retarget, or select an alternate.'
}

$emergencyInputCoordinator = Read-RequiredSource $emergencyInputCoordinatorPath 'Shared emergency-action input coordinator'
$normalizedEmergencyInputCoordinator = $emergencyInputCoordinator -replace '\s+', ' '
Assert-Literals $emergencyInputCoordinator @(
    'new GameInputContextProbe(keyState)',
    'probe.Observe()',
    'probe.ConsumeHeldGameplayKeys()',
    'FreshGameplayKeyPressed',
    'HeldGameplayKeyEligible',
    'HeldMovementKeyEligible',
    'IsConsumed',
    'if (IsConsumed) return',
    'purifyHeldEnabled',
    'defensiveUtilityHeldEnabled',
    'defensiveUtilityHeldWasEnabled',
    'paladinGuardianHeldEnabled',
    'paladinGuardianHeldWasEnabled',
    'smartRecuperateHeldEnabled',
    'smartRecuperateHeldWasEnabled',
    'allyRescueHeldEnabled',
    'miracleInterceptHeldEnabled',
    'miracleInterceptHeldWasEnabled',
    'scholarCriticalStrategyHeldEnabled',
    'scholarCriticalStrategyHeldWasEnabled',
    'pressureEscapeHeldEnabled',
    'pressureEscapeHeldWasEnabled',
    'darkKnightPlungeHeldEnabled',
    'darkKnightPlungeHeldWasEnabled',
    'ninjaGuardShukuchiHeldEnabled',
    'ninjaGuardShukuchiHeldWasEnabled',
    'ninjaSeitonHeldEnabled',
    'ninjaSeitonHeldWasEnabled',
    'IsGameplayKeyPhysicallyDown(VirtualKey key)',
    'IsGameplayKeyGenerationEligible(VirtualKey key)',
    'HeldMovementKey = Dalamud.Game.ClientState.Keys.VirtualKey.NO_KEY',
    'heldOptionJustEnabled',
    'probe.Reset()'
) 'Shared physical-hold input ownership for Purify > reactive CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > NIN Seiton > SCH > DRK Plunge > Smart Recuperate > generic Guard > pressure Sprint'
if ($normalizedEmergencyInputCoordinator -notmatch 'internal bool FreshGameplayKeyPressed => !IsConsumed && Snapshot\.ProbeSucceeded && Snapshot\.FreshGameplayKeyPressed;' -or
    $normalizedEmergencyInputCoordinator -notmatch 'internal bool HeldGameplayKeyEligible => !IsConsumed && Snapshot\.ProbeSucceeded && Snapshot\.HeldGameplayKeyEligible;' -or
    $normalizedEmergencyInputCoordinator -notmatch 'internal bool IsGameplayKeyPhysicallyDown\(VirtualKey key\) => Snapshot\.ProbeSucceeded && probe\?\.IsGameplayKeyPhysicallyDown\(key\) == true;' -or
    $normalizedEmergencyInputCoordinator -notmatch 'internal bool IsGameplayKeyGenerationEligible\(VirtualKey key\) => Snapshot\.ProbeSucceeded && !Snapshot\.IsTextInputActive && probe\?\.IsGameplayKeyGenerationEligible\(key\) == true;') {
    throw 'The shared frame must expose fresh/held gameplay keys only while that exact generation remains unconsumed, retain a separate physical-level observation, and revalidate exact generation eligibility with text input closed.'
}
if ($emergencyInputCoordinator -match '\bsmartKardiaHeld(?:Enabled|WasEnabled)\b') {
    throw 'Accepted-Eukrasia Smart Kardia must not participate in held-key generation tracking.'
}
if ($emergencyInputCoordinator -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|Hook<|HookFromAddress|ITargetManager|TargetManager)\b') {
    throw 'The shared emergency input coordinator may only observe and consume physical generations.'
}

$frameConsumeMethod = [regex]::Match(
    $normalizedEmergencyInputCoordinator,
    'internal void Consume\(\) \{(?<Body>.*?)\} internal bool FreshGameplayKeyPressed')
$enableEdgeMethod = [regex]::Match(
    $normalizedEmergencyInputCoordinator,
    'if \(heldOptionJustEnabled\) \{(?<Body>.*?)\} return new EmergencyActionInputFrame')
if (-not $frameConsumeMethod.Success -or
    $frameConsumeMethod.Groups['Body'].Value -notmatch 'if \(IsConsumed\) return; IsConsumed = true;' -or
    $frameConsumeMethod.Groups['Body'].Value -match '\bprobe\b|ConsumeHeldGameplayKeys' -or
    -not $enableEdgeMethod.Success -or
    $enableEdgeMethod.Groups['Body'].Value -notmatch 'probe\.ConsumeHeldGameplayKeys\(\)' -or
    $normalizedEmergencyInputCoordinator -notmatch '\(ninjaSeitonHeldEnabled && !ninjaSeitonHeldWasEnabled\).*?ninjaSeitonHeldWasEnabled = ninjaSeitonHeldEnabled;.*?if \(heldOptionJustEnabled\)') {
    throw 'Shared input consumption must be frame-local; physical generation priming is allowed only on a held-option enable edge and must include NIN.'
}

# Stable physical held-key ownership is shared by every physical-hold helper.
# An already-held movement key wins before another already-held key, and fresh
# keys are only deterministic fallbacks. The chosen eligible key is sticky
# across frames until it becomes ineligible, Reset, or explicit consumption.
$stablePhysicalKeyRules = Read-RequiredSource $physicalGameplayKeyRulesPath 'Stable physical gameplay-key rules'
$normalizedStablePhysicalKeyRules = $stablePhysicalKeyRules -replace '\s+', ' '
$stablePhysicalKeySelfTests = Read-RequiredSource $physicalGameplayKeySelfTestsPath 'Stable physical gameplay-key self-tests'
$stableInputProbe = Read-RequiredSource $inputContextPath 'Stable physical gameplay-key runtime probe'
$normalizedStableInputProbe = $stableInputProbe -replace '\s+', ' '
Assert-Literals $stablePhysicalKeyRules @(
    'public static int SelectPreferredHeldKeyToken(',
    'public static int RetainEligibleHeldKeyToken(',
    'return Math.Min(selectedKeyToken, candidateKeyToken);',
    '(false, true) => 0,',
    '(false, false) => 1,',
    '(true, true) => 2,',
    '_ => 3,'
) 'Stable movement/other/fresh held-key selection'
if ($normalizedStablePhysicalKeyRules -notmatch 'if \(candidateKeyToken <= 0\) return selectedKeyToken; if \(selectedKeyToken <= 0\) return candidateKeyToken;.*?if \(candidatePriority != selectedPriority\) return candidatePriority < selectedPriority \? candidateKeyToken : selectedKeyToken; return Math\.Min\(selectedKeyToken, candidateKeyToken\);' -or
    $normalizedStablePhysicalKeyRules -notmatch 'currentKeyToken > 0 && currentKeyStillEligible \? currentKeyToken : Math\.Max\(0, preferredFallbackKeyToken\);') {
    throw 'Stable held-key choice must prefer stable movement/stable other before fresh fallbacks, keep deterministic token ties, and retain the current exact eligible lease.'
}
Assert-Literals $stableInputProbe @(
    'private VirtualKey selectedHeldGameplayKey = VirtualKey.NO_KEY;',
    'private VirtualKey selectedHeldMovementKey = VirtualKey.NO_KEY;',
    'PhysicalGameplayKeyRules.SelectPreferredHeldKeyToken(',
    'PhysicalGameplayKeyRules.RetainEligibleHeldKeyToken(',
    'selectedHeldGameplayKeyStillEligible',
    'selectedHeldMovementKeyStillEligible',
    'selectedHeldGameplayKey = heldKey;',
    'selectedHeldMovementKey = heldMovementKey;',
    'internal bool IsGameplayKeyGenerationEligible(VirtualKey key)',
    'generation.IsEligible',
    '!generation.IsConsumed'
) 'Runtime stable held-key selection and retention'
if ($normalizedStableInputProbe -notmatch 'internal bool IsGameplayKeyGenerationEligible\(VirtualKey key\).*?var generation = keyGenerations\[index\]; return generation\.IsPrimed && generation\.IsDown && generation\.IsEligible && !generation\.IsConsumed;') {
    throw 'Runtime episode reservations must validate the exact observed physical generation, including eligibility and unconsumed state, rather than a raw key-down level.'
}
$stableInputReset = [regex]::Match(
    $normalizedStableInputProbe,
    'internal void Reset\(\) \{(?<Body>.*?)\} internal void ConsumeHeldGameplayKeys')
$stableInputConsume = [regex]::Match(
    $normalizedStableInputProbe,
    'internal void ConsumeHeldGameplayKeys\(\) \{(?<Body>.*?)\} /// <summary>')
foreach ($method in @($stableInputReset, $stableInputConsume)) {
    if (-not $method.Success -or
        $method.Groups['Body'].Value -notmatch 'selectedHeldGameplayKey = VirtualKey\.NO_KEY;' -or
        $method.Groups['Body'].Value -notmatch 'selectedHeldMovementKey = VirtualKey\.NO_KEY;') {
        throw 'Game input Reset and held-generation consumption must both clear the sticky gameplay and movement-key selections.'
    }
}
foreach ($method in @(
    'StableHoldWinsOverCoincidentFreshTap',
    'StableSelectionSurvivesMultiFrameActionTap',
    'TextInputPoisonsOnlyTheCurrentHold')) {
    Assert-Literals $stablePhysicalKeySelfTests @("public static void $method()") "Stable held-key self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("PhysicalGameplayKeySelfTests.$method") "Stable held-key test registration $method"
}
Assert-Literals $stablePhysicalKeySelfTests @(
    'typing is never a gameplay edge',
    'typed hold stays blocked after chat closes',
    'only the later generation is eligible'
) 'Exact text-input poisoning until physical release'

$purifyBufferRulesForLease = Read-RequiredSource $emergencyPurifyBufferRulesPath 'Purify held-before-fresh rules'
$allyRescueBufferRulesForLease = Read-RequiredSource $allyRescueBufferRulesPath 'Ally Rescue held-before-fresh rules'
$miracleRulesForLease = Read-RequiredSource $miracleInterceptRulesPath 'Reactive CC held-before-fresh rules'
$normalizedPurifyBufferRulesForLease = $purifyBufferRulesForLease -replace '\s+', ' '
$normalizedAllyRescueBufferRulesForLease = $allyRescueBufferRulesForLease -replace '\s+', ' '
$normalizedMiracleRulesForLease = $miracleRulesForLease -replace '\s+', ' '
$defensiveUtilityForLease = Read-RequiredSource $defensiveUtilityProbePath 'Guard and Guardian held-before-fresh runtime'
$miracleProbeForLease = Read-RequiredSource $miracleInterceptProbePath 'Reactive CC held-before-fresh runtime'
$normalizedMiracleProbeForLease = $miracleProbeForLease -replace '\s+', ' '
if ($normalizedPurifyBufferRulesForLease -notmatch 'if \(observation\.AllowHeldKeyAtStatusEntry && observation\.HeldKeyEligible && observation\.HeldKeyCode > 0\).*?return EmergencyPurifyInputTrigger\.HeldKeyAtStatusEntry;.*?if \(observation\.FreshKeyPressed && observation\.FreshKeyCode > 0\).*?return EmergencyPurifyInputTrigger\.FreshKeyPress;' -or
    $normalizedAllyRescueBufferRulesForLease -notmatch 'if \(observation\.AllowHeldKeyAtCandidateEntry && observation\.HeldKeyEligible && observation\.HeldGameplayKeyToken > 0\) return AllyRescueInputTrigger\.HeldKeyAtCandidateEntry; if \(observation\.FreshKeyPressed && observation\.FreshGameplayKeyToken > 0\) return AllyRescueInputTrigger\.FreshKeyPress;' -or
    $normalizedMiracleRulesForLease -notmatch 'if \(observation\.HeldKeyEligible\) return MiracleInterceptInputTrigger\.HeldPhysicalKey; if \(observation\.FreshKeyPressed\) return MiracleInterceptInputTrigger\.FreshKeyPress;' -or
    [regex]::Matches($defensiveUtilityForLease, 'var selectedKey = heldKey != VirtualKey\.NO_KEY \? heldKey : freshKey;').Count -ne 2 -or
    $normalizedMiracleProbeForLease -notmatch 'private int ResolveEpisodeGameplayKeyToken\( bool allowHeldGameplayKey, EmergencyActionInputFrame inputFrame\).*?if \(!input\.ProbeSucceeded \|\| input\.IsTextInputActive\) return 0;.*?if \(allowHeldGameplayKey && TryGetLatchedProtectionEndKey\(out var latchedKey\) && inputFrame\.IsGameplayKeyGenerationEligible\(latchedKey\)\).*?var candidate = allowHeldGameplayKey && input\.HeldGameplayKeyEligible \? input\.HeldGameplayKey : input\.FreshGameplayKeyPressed \? input\.FreshGameplayKey : VirtualKey\.NO_KEY;.*?IsExactVirtualKey\(candidate\) && inputFrame\.IsGameplayKeyGenerationEligible\(candidate\)') {
    throw 'Purify, Ally Rescue, reactive CC, Guard, and Guardian must evaluate their stable held lease before any coincident fresh-key fallback.'
}
Assert-Literals $coreSelfTestProgramForGuardian @(
    'EmergencyPurifyBufferSelfTests.StableHoldWinsWhenFreshAndHeldCoincide',
    'AllyRescueBufferSelfTests.StableHeldEntryWinsOverCoincidentFreshTap',
    'MiracleInterceptSelfTests.StableHoldWinsAndTypingNeverTriggers'
) 'Held-before-fresh helper regression registrations'

# One central coordinator may request the game's native cast cancellation for
# only the highest-priority otherwise-ready exact physical-hold intent. The
# native boundary returns void, so the state machine records a request rather
# than success and rearms only after both cast signals are clear.
$heldCastCancellationRules = Read-RequiredSource $heldCastCancellationRulesPath 'Held cast cancellation rules'
$normalizedHeldCastCancellationRules = $heldCastCancellationRules -replace '\s+', ' '
$heldCastCancellationService = Read-RequiredSource $heldCastCancellationServicePath 'Held cast cancellation native service'
$normalizedHeldCastCancellationService = $heldCastCancellationService -replace '\s+', ' '
$heldCastCancellationSelfTests = Read-RequiredSource $heldCastCancellationSelfTestsPath 'Held cast cancellation self-tests'
Assert-Literals $heldCastCancellationRules @(
    'public enum HeldCastCancellationHelperKind : byte',
    'None = 0,',
    'Purify = 1,',
    'ReactiveCounterCc = 2,',
    'AllyRescue = 3,',
    'Guardian = 4,',
    'NinjaGuardShukuchi = 5,',
    'NinjaSeiton = 6,',
    'ScholarCriticalStrategy = 7,',
    'DarkKnightPlunge = 8,',
    'SmartRecuperate = 9,',
    'Guard = 10,',
    'PressureEscapeSprint = 11,',
    'HeldCastCancellationRequest(',
    'TargetPressureActorIdentity LocalPlayer,',
    'TargetPressureActorIdentity Target,',
    'int FrozenKeyCode,',
    'ulong IntentEpochToken)',
    'LocalPlayer.IsValid',
    'Target.IsValid',
    'FrozenKeyCode > 0',
    'IntentEpochToken != 0',
    'public const float MaximumCancellationAnimationLockSeconds = 0.050f;',
    'HeldCastCancellationDecisionReason.CastSignalChangedWithoutClear',
    'HeldCastCancellationDecisionReason.LocalPlayerChanged',
    'next = next with { CancellationRequested = true };'
) 'Exact eleven-helper cast cancellation request and once-per-cast state'
if ($normalizedHeldCastCancellationRules -notmatch 'var anyCastSignal = observation\.LocalPlayerIsCasting \|\| observation\.CastActionId != 0; if \(!anyCastSignal\).*?CastEpochActive = false, CancellationRequested = false, CastSignalMismatch = false, ObservedCastActionId = 0, ObservedLocalPlayer = default, LocalPlayerIdentityMismatch = false,' -or
    $normalizedHeldCastCancellationRules -notmatch 'else if \(state\.ObservedCastActionId != 0 && observation\.CastActionId != 0 && state\.ObservedCastActionId != observation\.CastActionId\).*?CastSignalMismatch = true' -or
    $normalizedHeldCastCancellationRules -notmatch 'else if \(next\.ObservedLocalPlayer != observation\.CurrentLocalPlayer\).*?LocalPlayerIdentityMismatch = true' -or
    $normalizedHeldCastCancellationRules -notmatch 'if \(next\.CancellationRequested\) return Waiting\(next, HeldCastCancellationDecisionReason\.AlreadyRequested\);.*?if \(next\.CastSignalMismatch\).*?CastSignalChangedWithoutClear.*?if \(next\.LocalPlayerIdentityMismatch\).*?LocalPlayerChanged' -or
    $normalizedHeldCastCancellationRules -notmatch 'if \(observation\.HardReset\).*?if \(!observation\.FeatureEnabled\).*?if \(!observation\.SupportedContext\).*?if \(observation\.TextInputActive\).*?if \(observation\.GuardActive\).*?if \(!observation\.PrioritizedInputClaimed\).*?if \(observation\.Request is not \{ IsValid: true \} request\).*?if \(!observation\.IntentOtherwiseReady\).*?if \(!observation\.FrozenKeyStillDown\).*?if \(!observation\.LocalPlayerIdentityValid.*?if \(request\.LocalPlayer != observation\.CurrentLocalPlayer\).*?if \(!observation\.LocalPlayerAlive\).*?if \(!observation\.LocalPlayerTargetable\).*?if \(observation\.ResolvedHelperActionId != request\.HelperActionId\).*?if \(!observation\.HelperActionOffCooldown\).*?if \(!observation\.HelperActionResourcesReady\).*?if \(!observation\.LocalPlayerIsCasting \|\| observation\.CastActionId == 0\).*?if \(observation\.ActionQueued\).*?if \(!float\.IsFinite\(observation\.AnimationLockSeconds\) \|\| observation\.AnimationLockSeconds < 0f\).*?if \(observation\.AnimationLockSeconds > MaximumCancellationAnimationLockSeconds\)') {
    throw 'Central cast cancellation must fail closed across toggle, context, text, Guard, priority/claim, exact request/key/local/action/readiness/resources, dual cast signals, queue, finite lock, identity drift, and cast-ID drift.'
}

Assert-Literals $heldCastCancellationService @(
    'using FFXIVClientStructs.FFXIV.Client.Game.UI;',
    'HeldCastCancellationRequest? Request,',
    'HeldCastCancellationRequest? LastRequestedIntent,',
    'HeldCastCancellationNativeStatus NativeStatus,',
    'HeldCastCancellationNativeStatus LastNativeStatus,',
    'private HeldCastCancellationRequest? lastRequestedIntent;',
    'private HeldCastCancellationNativeStatus lastNativeStatus;',
    'prioritizedInputClaimed && inputFrame.IsConsumed',
    'inputFrame.IsGameplayKeyPhysicallyDown(',
    'ClientActionAttemptBoundary.Capture(',
    'ResolvedHelperActionId: boundary.AdjustedActionId',
    'HelperActionOffCooldown: boundary.Captured && boundary.IsActionOffCooldown',
    'HelperActionResourcesReady: boundary.Captured && boundary.ResourceStatus == 0',
    'LocalPlayerIsCasting: localPlayer?.IsCasting == true',
    'CastActionId: actionManager == null ? 0 : actionManager->CastActionId',
    'ActionQueued: actionManager == null || actionManager->ActionQueued',
    'state = decision.NextState;',
    'if (decision.ShouldInvokeNative)',
    'lastRequestedIntent = request;',
    'var uiState = UIState.Instance();',
    'uiState->Hotbar.CancelCast();',
    'lastNativeStatus = nativeStatus;',
    'request,',
    'lastRequestedIntent,',
    'nativeStatus,',
    'lastNativeStatus,',
    'localPlayer.CurrentHp > 0 &&',
    'localPlayer.MaxHp >= localPlayer.CurrentHp;'
) 'Central void native cast-cancel boundary and truthful persistent diagnostics'
if ($normalizedHeldCastCancellationService -notmatch 'var decision = HeldCastCancellationRules\.Observe\(state, observation\);.*?state = decision\.NextState;.*?if \(decision\.ShouldInvokeNative\).*?lastRequestedIntent = request;.*?var uiState = UIState\.Instance\(\);.*?if \(uiState == null\).*?NativeBoundaryUnavailable.*?else \{ uiState->Hotbar\.CancelCast\(\); nativeStatus = HeldCastCancellationNativeStatus\.Requested; nativeRequestCount\+\+; \}.*?catch \(Exception exception\).*?RequestFaulted.*?lastNativeStatus = nativeStatus;' -or
    $normalizedHeldCastCancellationService -notmatch 'new HeldCastCancellationSnapshot\( decision\.Kind, decision\.Reason, decision\.NextState\.LastCastEpochToken, request, lastRequestedIntent, observation\.CastActionId, nativeStatus, lastNativeStatus, nativeRequestCount, nativeFaultCount,' -or
    $heldCastCancellationService -match '\b(UseAction|UseActionLocation|SendInput|keybd_event|mouse_event|ExecuteCommand|QueueAction|ClearActionQueue|Jump|MovePlayer)\s*\(|VirtualKey\.(?:ESCAPE|SPACE|W|A|S|D)\b|->(?:CastActionId|AnimationLock|ActionQueued|QueuedActionId|QueuedTargetId)\s*=(?!=|>)') {
    throw 'The cast-cancel service must latch before exactly one void Hotbar request, keep current versus last-request diagnostics separate, and never use/inject an action, Escape, movement, jump, queue write, or cast-field mutation.'
}
$cancelCastCalls = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '(?-i)\bCancelCast\s*\(')
if ($cancelCastCalls.Count -ne 1 -or
    $cancelCastCalls[0].Path -ne $heldCastCancellationServicePath -or
    $cancelCastCalls[0].Line -notmatch '^\s*uiState->Hotbar\.CancelCast\(\);\s*$') {
    $locations = $cancelCastCalls | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "UIState.Instance()->Hotbar.CancelCast() must remain the sole native cast-cancel call: $($locations -join ', ')"
}
$castFieldWrites = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '->(?:CastActionId|AnimationLock|ActionQueued|QueuedActionType|QueuedActionId|QueuedTargetId|QueuedExtraParam|QueueType|QueuedComboRouteId)\s*=(?!=|>)')
if ($castFieldWrites.Count -ne 0) {
    $locations = $castFieldWrites | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Native cast, animation-lock, or action-queue fields must remain read-only: $($locations -join ', ')"
}

$heldCastCancellationTestMethods = @(
    'CanonicalHelperPriorityOrderIsPinned',
    'ExactRequestIsOncePerObservedCastEpoch',
    'IntentMayBecomeEligibleInsideTheSameCast',
    'OnlyConsistentClearRearmsAndSignalDriftFailsClosed',
    'EveryCentralSafetyGateFailsClosed',
    'RequestIdentityAndLockBoundaryAreExact',
    'TerminalRequestSurvivesLaterGateChanges'
)
foreach ($method in $heldCastCancellationTestMethods) {
    Assert-Literals $heldCastCancellationSelfTests @("internal static void $method()") "Held cast cancellation self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("HeldCastCancellationSelfTests.$method") "Held cast cancellation test registration $method"
}
if ([regex]::Matches($heldCastCancellationSelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 7 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bHeldCastCancellationSelfTests\.\w+').Count -ne 7) {
    throw 'All seven canonical-order, once-per-cast, dual-clear, drift-quarantine, exact-boundary, and central-gate cast-cancellation tests must remain registered exactly once.'
}

$castRequestProducers = @(
    [pscustomobject]@{ Path = $purifyProbePath; Kind = 'Purify'; Count = 1 },
    [pscustomobject]@{ Path = $miracleInterceptProbePath; Kind = 'ReactiveCounterCc'; Count = 1 },
    [pscustomobject]@{ Path = $allyRescueProbePath; Kind = 'AllyRescue'; Count = 1 },
    [pscustomobject]@{ Path = $defensiveUtilityProbePath; Kind = 'Guardian'; Count = 1 },
    [pscustomobject]@{ Path = $ninjaGuardShukuchiProbePath; Kind = 'NinjaGuardShukuchi'; Count = 1 },
    [pscustomobject]@{ Path = $ninjaSeitonProbePath; Kind = 'NinjaSeiton'; Count = 1 },
    [pscustomobject]@{ Path = $scholarCriticalStrategyProbePath; Kind = 'ScholarCriticalStrategy'; Count = 1 },
    [pscustomobject]@{ Path = $darkKnightPlungeProbePath; Kind = 'DarkKnightPlunge'; Count = 1 },
    [pscustomobject]@{ Path = $smartRecuperateProbePath; Kind = 'SmartRecuperate'; Count = 1 },
    [pscustomobject]@{ Path = $defensiveUtilityProbePath; Kind = 'Guard'; Count = 1 },
    [pscustomobject]@{ Path = $pressureEscapeSprintProbePath; Kind = 'PressureEscapeSprint'; Count = 1 }
)
$castRequestProducerPaths = @($castRequestProducers.Path | Sort-Object -Unique)
$allCastRequestProducerSource = ($castRequestProducerPaths | ForEach-Object {
    Read-RequiredSource $_ "Held cast cancellation request producer $_"
}) -join "`n"
if ([regex]::Matches($allCastRequestProducerSource, '\bnew HeldCastCancellationRequest\s*\(').Count -ne 11) {
    throw 'Production runtime must construct exactly eleven cast-cancellation request shapes, one for each physical-hold helper.'
}
foreach ($producer in $castRequestProducers) {
    $producerSource = Read-RequiredSource $producer.Path "Cast-cancellation producer $($producer.Kind)"
    $kindPattern = "HeldCastCancellationHelperKind\.$([regex]::Escape($producer.Kind))\s*,"
    if ([regex]::Matches($producerSource, $kindPattern).Count -ne $producer.Count -or
        $producerSource -notmatch '\bCastCancellationRequest\b' -or
        $producerSource -notmatch '\b(?:FrozenKeyCode|FrozenKey|HeldKey|GameplayKeyToken)\b' -or
        $producerSource -notmatch '\b(?:IntentEpochToken|InstanceToken|HealthEventToken|WarningEpisodeToken|GetIntentEpochToken)\b' -or
        $producerSource -notmatch 'IsGameplayKeyPhysicallyDown') {
        throw "$($producer.Kind) must expose exactly one exact local/action/target/frozen-key/nonzero-epoch cast-cancellation request path."
    }
}

$heldCastPersonalStatus = Read-RequiredSource $personalStatusPath 'Canonical held cast cancellation coordinator'
$normalizedHeldCastPersonalStatus = $heldCastPersonalStatus -replace '\s+', ' '
$castSelection = [regex]::Match(
    $normalizedHeldCastPersonalStatus,
    'var castCancellationRequest =(?<Body>.*?)heldCastCancellation\.Observe\(')
if (-not $castSelection.Success -or
    [regex]::Matches($castSelection.Groups['Body'].Value, 'ClaimedCastCancellationRequest\(').Count -ne 11 -or
    $castSelection.Groups['Body'].Value -notmatch 'purify\.InputClaimed, purify\.CastCancellationRequest\).*?ninja\.InputClaimed, ninja\.CastCancellationRequest\).*?miracle\.InputClaimed, miracle\.CastCancellationRequest\).*?rescue\.InputClaimed, rescue\.CastCancellationRequest\).*?defense\.InputClaimed, defense\.CastCancellationRequest\).*?guardShukuchi\.InputClaimed, guardShukuchi\.CastCancellationRequest\).*?scholar\.InputClaimed, scholar\.CastCancellationRequest\).*?plunge\.InputClaimed, plunge\.CastCancellationRequest\).*?recuperate\.InputClaimed, recuperate\.CastCancellationRequest\).*?guardDefense\.InputClaimed, guardDefense\.CastCancellationRequest\).*?pressureEscape\.InputClaimed, pressureEscape\.CastCancellationRequest\)' -or
    $castSelection.Groups['Body'].Value -match '\b(kardia|monk)\b') {
    throw 'PersonalStatus must select exactly one cast-cancel request in canonical Purify > NIN Seiton > reactive CC > Rescue > Guardian > Guard-Shukuchi > SCH > DRK > Recuperate > Guard > Sprint order, excluding Kardia and Monk.'
}
Assert-Literals $heldCastPersonalStatus @(
    'cast-cancel request owns this frame; the normal UseAction boundary is',
    'deliberately reached no earlier than a later clear-cast frame.',
    'configuration.AllowHeldHelpersToCancelOwnCast',
    'prioritizedInputClaimed: castCancellationRequest is { IsValid: true }',
    'intentOtherwiseReady: castCancellationRequest is { IsValid: true }',
    'request: castCancellationRequest',
    'inputClaimed && request is { IsValid: true }'
) 'Canonical one-request-per-frame held cast cancellation selection'
foreach ($excludedPath in @(
    $smartKardiaProbePath,
    $monkEarthReplyProbePath,
    $smartWardensPaeanServicePath,
    $nearAssistPath,
    $darkKnightShadowbringerServicePath)) {
    $excludedSource = Read-RequiredSource $excludedPath "Cast-cancellation-excluded runtime $excludedPath"
    if ($excludedSource -match '\b(HeldCastCancellationRequest|HeldCastCancellationHelperKind|CancelCast)\b') {
        throw "Kardia, Monk, manual/Turbo redirects including Paean, and macro helpers must remain outside cast cancellation: $excludedPath"
    }
}

$castConfigurationPath = Join-Path $sourceRoot 'SeitonSense.Plugin\Models\PluginConfiguration.cs'
$castConfiguration = Read-RequiredSource $castConfigurationPath 'Held cast cancellation configuration'
$normalizedCastConfiguration = $castConfiguration -replace '\s+', ' '
if ($castConfiguration -notmatch '(?m)^\s*public bool AllowHeldHelpersToCancelOwnCast \{ get; set; \}\s*$' -or
    $castConfiguration -match '(?m)^\s*public bool AllowHeldHelpersToCancelOwnCast \{ get; set; \}\s*=\s*true;' -or
    [regex]::Matches($castConfiguration, '\bAllowHeldHelpersToCancelOwnCast\s*=\s*false\s*;').Count -ne 2 -or
    $normalizedCastConfiguration -notmatch 'if \(Version < 30\).*?AllowHeldHelpersToCancelOwnCast = false;' -or
    $normalizedCastConfiguration -notmatch 'public void ResetToDefaults\(\).*?Version = 32;.*?AllowHeldHelpersToCancelOwnCast = false;') {
    throw 'Schema 32 must preserve held-helper cast cancellation as plain default-false, force it off for pre-30 upgrades, and restore it off on Reset Defaults.'
}

$settingsActionsPath = Join-Path $settingsPartsRoot 'SettingsWindow.Actions.cs'
$settingsDiagnosticsPath = Join-Path $settingsPartsRoot 'SettingsWindow.Diagnostics.cs'
$settingsActions = Read-RequiredSource $settingsActionsPath 'Held cast cancellation settings copy'
$settingsDiagnostics = Read-RequiredSource $settingsDiagnosticsPath 'Held cast cancellation diagnostics'
Assert-Literals $settingsActions @(
    'Cancel my active cast for an otherwise-ready held helper',
    'Default off.',
    'highest-priority held helper',
    'cancel exactly once for that observed cast',
    'never synthesizes movement or Escape',
    'clears a queued "',
    'changes a target',
    'combines cancel and UseAction in the same framework frame',
    'BRD Powerful Shot / MCH Blast Charge',
    'current-patch in-game behavior still needs live testing'
) 'Default-off held cast cancellation warning copy'
Assert-Literals $settingsDiagnostics @(
    'current-helper={castCancellation.Request?.HelperKind ?? HeldCastCancellationHelperKind.None}',
    'last-helper={castCancellation.LastRequestedIntent?.HelperKind ?? HeldCastCancellationHelperKind.None}',
    'last-action={castCancellation.LastRequestedIntent?.HelperActionId ?? 0}',
    'last-target={castCancellation.LastRequestedIntent?.Target.GameObjectId ?? 0:X}',
    'last-key={castCancellation.LastRequestedIntent?.FrozenKeyCode ?? 0}',
    'last-intent={castCancellation.LastRequestedIntent?.IntentEpochToken ?? 0}',
    'native/last-native={castCancellation.NativeStatus}/{castCancellation.LastNativeStatus}',
    'requested/faulted='
) 'Current decision plus persistent last actual cast-cancel request/native diagnostics'
Assert-Literals $pluginSource @(
    'current-helper={castCancellation.Request?.HelperKind ?? HeldCastCancellationHelperKind.None}',
    'last-helper={castCancellation.LastRequestedIntent?.HelperKind ?? HeldCastCancellationHelperKind.None}',
    'last-action={castCancellation.LastRequestedIntent?.HelperActionId ?? 0}',
    'last-target={castCancellation.LastRequestedIntent?.Target.GameObjectId ?? 0:X}',
    'last-key={castCancellation.LastRequestedIntent?.FrozenKeyCode ?? 0}',
    'last-intent={castCancellation.LastRequestedIntent?.IntentEpochToken ?? 0}',
    'native/last-native={castCancellation.NativeStatus}/{castCancellation.LastNativeStatus}'
) '/seiton debug current decision plus persistent last actual cast-cancel request/native diagnostics'

# One common held-action contract owns native classification and bounded retry.
$clientActionAttemptOutcome = Read-RequiredSource $clientActionAttemptOutcomePath 'Client action attempt outcome rules'
$normalizedClientActionAttemptOutcome = $clientActionAttemptOutcome -replace '\s+', ' '
$clientActionAttemptBoundary = Read-RequiredSource $clientActionAttemptBoundaryPath 'Client action attempt boundary'
$heldActionRetryRules = Read-RequiredSource $heldActionRetryRulesPath 'Held action retry rules'
$normalizedHeldActionRetryRules = $heldActionRetryRules -replace '\s+', ' '
$heldActionRetrySelfTests = Read-RequiredSource $heldActionRetrySelfTestsPath 'Held action retry self-tests'
Assert-Literals $clientActionAttemptOutcome @(
    'ClientRejected = 2',
    'ClientAccepted = 3',
    'AcceptanceUnknown = 4',
    'SoftUnavailable = 5',
    'bool ActionQueued,',
    'uint QueuedActionType,',
    'uint QueuedActionId,',
    'ulong QueuedTargetId,',
    'uint QueuedExtraParam,',
    'uint QueueMode,',
    'uint QueuedComboRouteId,',
    'ushort LastUsedActionSequence,',
    'float AnimationLockSeconds,',
    'uint CastActionId,',
    'uint AdjustedActionId,',
    'bool IsActionOffCooldown,',
    'uint ResourceStatus',
    'before == after',
    '? ClientActionAttemptOutcome.ClientRejected',
    ': ClientActionAttemptOutcome.AcceptanceUnknown'
) 'Complete native attempt fingerprint and clean-false-only classification'
Assert-Literals $clientActionAttemptBoundary @(
    'actionManager->ActionQueued',
    '(uint)actionManager->QueuedActionType',
    'actionManager->QueuedActionId',
    '(ulong)actionManager->QueuedTargetId',
    'actionManager->QueuedExtraParam',
    '(uint)actionManager->QueueType',
    'actionManager->QueuedComboRouteId',
    'actionManager->LastUsedActionSequence',
    'actionManager->AnimationLock',
    'actionManager->CastActionId',
    'actionManager->GetAdjustedActionId(actionId)',
    'actionManager->IsActionOffCooldown(ActionType.Action, actionId)',
    'actionManager->CheckActionResources(ActionType.Action, actionId)'
) 'Complete runtime native attempt fingerprint capture'
Assert-Literals $heldActionRetryRules @(
    'NativeRetryThrottleMilliseconds = 50',
    'MaximumNativeAttempts = 8',
    'MaximumNearQueueableAnimationLockSeconds = 0.050f',
    'ClientActionAttemptOutcome.ClientAccepted =>',
    'Terminal(HeldActionRetryDisposition.AcceptedTerminal)',
    'ClientActionAttemptOutcome.AcceptanceUnknown =>',
    'Terminal(HeldActionRetryDisposition.AmbiguousTerminal)',
    'ClientActionAttemptOutcome.ClientRejected =>',
    'CompleteRejected(previous, nowMilliseconds)',
    'ClientActionAttemptOutcome.SoftUnavailable =>',
    'new HeldActionRetryDecision(previous, HeldActionRetryDisposition.SoftWait)',
    'public static bool RetainsSchedulerFrame(',
    'bool actionSpecificReady,',
    'bool targetSpecificReady = true',
    'actionSpecificReady &&',
    'targetSpecificReady &&',
    'disposition is HeldActionRetryDisposition.RejectedTerminal or',
    'HeldActionRetryDisposition.AmbiguousTerminal'
) 'Shared 50-ms/eight-attempt retry, zero-budget soft wait, priority retention, and circuit breaker'
if ($normalizedClientActionAttemptOutcome -notmatch 'if \(clientReturnedAccepted\) return ClientActionAttemptOutcome\.ClientAccepted; return before\.IsExactActionReady\(expectedActionId\) && after\.IsExactActionReady\(expectedActionId\) && before == after \? ClientActionAttemptOutcome\.ClientRejected : ClientActionAttemptOutcome\.AcceptanceUnknown;' -or
    $normalizedHeldActionRetryRules -notmatch 'public static bool ShouldLatchHeldKeyUntilRelease\( HeldActionRetryDisposition disposition\) => disposition is HeldActionRetryDisposition\.RejectedTerminal or HeldActionRetryDisposition\.AmbiguousTerminal;' -or
    $heldActionRetryRules -match 'ShouldLatchHeldKeyUntilRelease[\s\S]{0,300}(?:AcceptedTerminal|CancelledTerminal)') {
    throw 'True and ambiguous native outcomes must be terminal; only exhaustion or ambiguity may latch the exact held key until release.'
}
$heldActionRetryTestMethods = @(
    'ProvenFalseRetriesAreThrottledAndBounded',
    'OnlyProvenFalseCanRetainTheFrozenIntent',
    'NativeFalseRequiresAStableReadyBoundaryFingerprint',
    'AcceptedEpisodeDoesNotLatchAContinuousHeldKey',
    'FrozenThrottleAndGlobalWaitRetainOnlyEligiblePriority',
    'InitialExactIntentClaimsCastSoftWaitWithoutSpendingBudget'
)
foreach ($method in $heldActionRetryTestMethods) {
    Assert-Literals $heldActionRetrySelfTests @("internal static void $method()") "Held action retry self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("HeldActionRetrySelfTests.$method") "Held action retry test registration $method"
}
if ([regex]::Matches($heldActionRetrySelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 6 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bHeldActionRetrySelfTests\.\w+').Count -ne 6) {
    throw 'All six shared retry/classification/priority-retention/cast-soft-wait tests must remain registered exactly once.'
}
$heldNativeRetryProbePaths = @(
    $purifyProbePath,
    $smartRecuperateProbePath,
    $allyRescueProbePath,
    $miracleInterceptProbePath,
    $defensiveUtilityProbePath,
    $pressureEscapeSprintProbePath,
    $ninjaSeitonProbePath,
    $scholarCriticalStrategyProbePath,
    $darkKnightPlungeProbePath
)
foreach ($path in $heldNativeRetryProbePaths) {
    $heldProbe = Read-RequiredSource $path "Held native action probe $path"
    if ($heldProbe -notmatch '\bClientActionAttemptBoundary\.Capture\s*\(' -or
        $heldProbe -notmatch '\bClientActionAttemptBoundaryRules\.Classify\s*\(' -or
        $heldProbe -notmatch '\bUseAction\s*\(' -or
        $heldProbe -match '\b(?:ITargetManager|TargetManager|SetTarget|AlternateAction|AlternateTarget|FallbackAction|FallbackTarget)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=(?!=|>)') {
        throw "Every held native helper must use the shared fingerprint/classifier for one frozen exact action/target with no alternate or target mutation: $path"
    }
}
$priorityRetainingProbePaths = @(
    $defensiveUtilityProbePath,
    $pressureEscapeSprintProbePath,
    $ninjaSeitonProbePath,
    $scholarCriticalStrategyProbePath,
    $darkKnightPlungeProbePath
)
foreach ($path in $priorityRetainingProbePaths) {
    $heldProbe = Read-RequiredSource $path "Held priority-retaining probe $path"
    if ($heldProbe -notmatch '\bHeldActionRetryRules\.RetainsSchedulerFrame\s*\(') {
        throw "Frozen throttle/global-boundary waits must retain the scheduler frame in $path"
    }
}

$personalStatus = Read-RequiredSource $personalStatusPath 'Personal status coordinator'
$normalizedPersonalStatus = $personalStatus -replace '\s+', ' '
$purifyObserve = [regex]::Match($personalStatus, '\bemergencyPurify\.Observe\s*\(')
$guardObserve = [regex]::Match($personalStatus, '\bdefensiveUtility\.ObserveGuard\s*\(')
$smartRecuperateObserve = [regex]::Match($personalStatus, '\bsmartRecuperate\.Observe\s*\(')
$guardianObserve = [regex]::Match($personalStatus, '\bdefensiveUtility\.ObserveGuardian\s*\(')
$guardianCommunicationObserve = [regex]::Match($personalStatus, '\bguardianCommunication\.Observe\s*\(')
$pressureEscapeObserve = [regex]::Match($personalStatus, '\bpressureEscapeSprint\.Observe\s*\(')
$rescueObserve = [regex]::Match($personalStatus, '\ballyRescue\.Observe\s*\(')
$miracleObserve = [regex]::Match($personalStatus, '\bmiracleIntercept\.Observe\s*\(')
$smartKardiaObserve = [regex]::Match($personalStatus, '\bsmartKardia\.Observe\s*\(')
$ninjaSeitonObserve = [regex]::Match($personalStatus, '\bninjaSeiton\.Observe\s*\(')
$ninjaGuardShukuchiObserve = [regex]::Match($personalStatus, '\bninjaGuardShukuchi\.Observe\s*\(')
$scholarCriticalStrategyObserve = [regex]::Match($personalStatus, '\bscholarCriticalStrategy\.Observe\s*\(')
$monkEarthReplyObserve = [regex]::Match($personalStatus, '\bmonkEarthReply\.Observe\s*\(')
$darkKnightPlungeObserve = [regex]::Match($personalStatus, '\bdarkKnightPlunge\.Observe\s*\(')
if (-not $purifyObserve.Success -or -not $guardObserve.Success -or -not $smartRecuperateObserve.Success -or -not $guardianObserve.Success -or -not $guardianCommunicationObserve.Success -or -not $pressureEscapeObserve.Success -or -not $rescueObserve.Success -or
    -not $miracleObserve.Success -or -not $smartKardiaObserve.Success -or -not $ninjaSeitonObserve.Success -or -not $ninjaGuardShukuchiObserve.Success -or -not $scholarCriticalStrategyObserve.Success -or -not $monkEarthReplyObserve.Success -or -not $darkKnightPlungeObserve.Success -or
    $purifyObserve.Index -gt $ninjaSeitonObserve.Index -or
    $ninjaSeitonObserve.Index -gt $miracleObserve.Index -or
    $miracleObserve.Index -gt $rescueObserve.Index -or
    $rescueObserve.Index -gt $guardianObserve.Index -or
    $guardianObserve.Index -gt $guardianCommunicationObserve.Index -or
    $guardianCommunicationObserve.Index -gt $ninjaGuardShukuchiObserve.Index -or
    $ninjaGuardShukuchiObserve.Index -gt $scholarCriticalStrategyObserve.Index -or
    $scholarCriticalStrategyObserve.Index -gt $darkKnightPlungeObserve.Index -or
    $darkKnightPlungeObserve.Index -gt $smartRecuperateObserve.Index -or
    $smartRecuperateObserve.Index -gt $guardObserve.Index -or
    $guardObserve.Index -gt $pressureEscapeObserve.Index -or
    $pressureEscapeObserve.Index -gt $smartKardiaObserve.Index -or
    $smartKardiaObserve.Index -gt $monkEarthReplyObserve.Index -or
    [regex]::Matches($personalStatus, '\bemergencyInputFrame\b').Count -lt 7) {
    throw 'Personal status coordination must process Purify, NIN Seiton, reactive CC, Ally Rescue, Guardian, same-frame Guardian communication, Guard-Shukuchi, SCH, DRK Plunge, Smart Recuperate, generic Guard, pressure Sprint, event Kardia, then event Monk in exact order.'
}
Assert-Literals $personalStatus @(
    'var isPaladin = localJobId == EnemyCombatConstants.PaladinJobId;',
    'var isAllyRescueJob = localJobId is EnemyCombatConstants.WhiteMageJobId or',
    'EnemyCombatConstants.BardJobId;',
    'var isNinja = ExecuteThreshold.IsNinja(localJobId);',
    'var isSage = localJobId == SmartKardiaRules.SageJobId;',
    'var isScholar = localJobId == ScholarCriticalStrategyRules.ScholarJobId;',
    'var isMonk = localJobId == MonkEarthReplyRules.MonkJobId;',
    'var isDarkKnight = localJobId == DarkKnightPlungeRules.DarkKnightJobId;',
    'purifyClaimedPriority',
    'defensiveUtilityClaimedPriority',
    'defensiveUtilitiesConfigurationEnabled',
    'configuration.EnableDefensiveUtilities',
    'configuration.GuardOnStunPressure',
    'configuration.PaladinGuardianLowAlly',
    'configuration.PaladinGuardianOnHeldKey',
    'configuration.EnableSmartRecuperateOnHeldKey',
    'new SmartRecuperateProbe(',
    'SmartRecuperateProbeSnapshot SmartRecuperateDiagnostics',
    'smartRecuperate.Observe(',
    'metadata.RecuperateVerified',
    'new GuardianCommunicationService(',
    'GuardianCommunicationDiagnostics GuardianCommunicationDiagnostics',
    'guardianCommunication.Observe(',
    'defense.LastAcceptedGuardianEpisode',
    'guardianCommunication.TryClearOneExactOwnershipOnDispose(',
    'guardianCommunication.FailClosed(now, exception)',
    'guardianCommunication.Reset()',
    'new PressureEscapeSprintProbe(',
    'PressureEscapeSprintProbeSnapshot PressureEscapeDiagnostics',
    'pressureEscapeSprint.Observe(',
    'pressureEscapeSprint.FailClosed(now, exception)',
    'pressureEscapeSprint.Reset()',
    'pressureEscapeClaimedPriority',
    'configuration.ShowHighPressureWarning',
    'configuration.PlayHighPressureWarningSound',
    'configuration.EnablePressureEscapeSprintOnHeldKey',
    'purify.UseActionAttempted',
    'resilienceActive',
    'guardActive',
    'miracleInterceptConfigurationEnabled,',
    '!purifyClaimedPriority &&',
    '!miracle.InputClaimed &&',
    'metadata.AllyRescueStatusesVerified',
    'metadata.MiracleOfNatureActionVerified',
    'metadata.MarksmanSpiteVerified',
    'metadata.ZantetsukenVerified',
    'metadata.FuriousBacklashVerified',
    'configuration.EnableSageKardiaAfterEukrasia',
    'smartKardiaConfigurationEnabled',
    'new SmartKardiaProbe(',
    'SmartKardiaProbeSnapshot SmartKardiaDiagnostics',
    'metadata.SmartKardiaVerified',
    'smartKardia.Observe(',
    'kardia.UseActionAttempted',
    'smartKardia.FailClosed(',
    'smartKardia.Reset()',
    'configuration.EnableReactiveCcUtilities',
    'configuration.ReactiveCcOnHeldKey',
    'configuration.ReactiveCcDancerLimitBreak',
    'configuration.ReactiveCcAfterEnemyPurify',
    'configuration.ReactiveCcAfterEnemyGuard',
    'configuration.EnableNinjaGuardShukuchiOnHeldGameplayKey',
    'ninjaGuardShukuchiConfigurationEnabled',
    'new NinjaGuardShukuchiProbe(',
    'NinjaGuardShukuchiProbeSnapshot NinjaGuardShukuchiDiagnostics',
    'metadata.PanicShukuchiVerified && metadata.GuardVerified',
    'ninjaGuardShukuchi.Observe(',
    'guardShukuchi.InputClaimed',
    'ninjaGuardShukuchi.FailClosed()',
    'ninjaGuardShukuchi.Reset()',
    'configuration.EnableNinjaSeitonOnHeldGameplayKey',
    'ninjaSeitonConfigurationEnabled',
    'new NinjaSeitonDispatchProbe(',
    'NinjaSeitonDispatchProbeSnapshot NinjaSeitonDiagnostics',
    'ninjaSeiton.FailClosed()',
    'ninjaSeiton.Reset()',
    'metadata.SeitonVerified',
    'ninjaSeiton.Observe(',
    'ninja.InputClaimed',
    'configuration.EnableScholarCriticalStrategyOnHeldKey',
    'scholarCriticalStrategyHeldInputEnabled',
    'new ScholarCriticalStrategyProbe(',
    'ScholarCriticalStrategyProbeSnapshot ScholarCriticalStrategyDiagnostics',
    'metadata.ScholarCriticalStrategyVerified',
    'scholarCriticalStrategy.Observe(',
    'scholar.InputClaimed',
    'scholarCriticalStrategy.FailClosed()',
    'scholarCriticalStrategy.Reset()',
    'configuration.EnableDarkKnightPlungeOnHeldKey',
    'darkKnightPlungeConfigurationEnabled',
    'darkKnightPlungeHeldInputEnabled',
    'metadata.DarkKnightPlungeVerified',
    'darkKnightPlunge.Observe(',
    'darkKnightPlunge.Reset()',
    'metadata.PurifyVerified',
    'context == SupportedPvPContext.CrystallineConflict'
) 'Exact shared priority from self-Purify through the job-specific DRK Plunge tier'
Assert-Literals $personalStatus @(
    'var purifyHeldInputEnabled = configuration.Enabled &&',
    'var defensiveUtilityHeldInputEnabled = defensiveUtilitiesConfigurationEnabled &&',
    'var paladinGuardianHeldInputEnabled = paladinGuardianConfigurationEnabled &&',
    'var smartRecuperateHeldInputEnabled = configuration.Enabled &&',
    'var allyRescueHeldInputEnabled = configuration.Enabled &&',
    'var miracleInterceptHeldInputEnabled = configuration.Enabled &&',
    'var scholarCriticalStrategyHeldInputEnabled =',
    'var pressureEscapeSprintHeldInputEnabled = configuration.Enabled &&',
    'var darkKnightPlungeHeldInputEnabled = darkKnightPlungeConfigurationEnabled &&',
    'var ninjaGuardShukuchiHeldInputEnabled =',
    'var ninjaSeitonHeldInputEnabled = ninjaSeitonConfigurationEnabled &&',
    'var anyPersistentHeldInputEnabled = purifyHeldInputEnabled ||',
    'ninjaGuardShukuchiHeldEnabled: ninjaGuardShukuchiHeldInputEnabled',
    'ninjaSeitonHeldEnabled: ninjaSeitonHeldInputEnabled'
) 'Guard-independent persistent physical held-input observation gates'
if ($normalizedPersonalStatus -notmatch 'miracleIntercept = new MiracleInterceptProbe\( objectTable, nearAssist\.VerifiedCcBrakeActionIds, nearAssist\.VerifiedCcBrakeStatusIds, executeTracker, pressureTracker, nearAssist, machinistLimitBreakCapture, log, metadata\);' -or
    $normalizedPersonalStatus -notmatch 'var isAllyRescueJob = localJobId is EnemyCombatConstants\.WhiteMageJobId or EnemyCombatConstants\.BardJobId; var isNinja = ExecuteThreshold\.IsNinja\(localJobId\); var isReactiveCcJob = isAllyRescueJob \|\| isNinja;' -or
    $normalizedPersonalStatus -notmatch 'var reactiveCcActionMetadataVerified = \(localJobId == EnemyCombatConstants\.WhiteMageJobId && metadata\.MiracleOfNatureActionVerified\) \|\| \(localJobId == EnemyCombatConstants\.BardJobId && metadata\.SilentNocturneVerified\) \|\| \(localJobId == EnemyCombatConstants\.NinjaJobId && nearAssist\.VerifiedCcBrakeActionIds\.Contains\( EnemyCombatConstants\.ForkedRaijuActionId\) && nearAssist\.VerifiedCcBrakeActionIds\.Contains\( EnemyCombatConstants\.FleetingRaijuActionId\)\);.*?var miracleInterceptHeldInputEnabled = configuration\.Enabled && configuration\.EnableReactiveCcUtilities && configuration\.ReactiveCcOnHeldKey && reactiveCcActionMetadataVerified && isCrystallineConflict && isReactiveCcJob;') {
    throw 'Reactive counter-CC must remain WHM/BRD/NIN-only, pass the verified brake catalogs into runtime, and require both Raiju metadata rows with AND before NIN held observation can arm.'
}
$persistentHeldGateBlock = [regex]::Match(
    $normalizedPersonalStatus,
    'var purifyHeldInputEnabled =(?<Body>.*?)var emergencyInputFrame = emergencyInput\.Observe')
$guardHoldSelfTests = Read-RequiredSource (
    Join-Path $coreSelfTestRoot 'PhysicalGameplayKeySelfTests.cs') 'Physical gameplay key self-tests'
if (-not $persistentHeldGateBlock.Success -or
    $persistentHeldGateBlock.Groups['Body'].Value -match '\bguardActive\b' -or
    $guardHoldSelfTests -notmatch '\bpublic static void GuardSuppressionPreservesObservedHold\s*\(' -or
    $coreSelfTestProgramForGuardian -notmatch '\bPhysicalGameplayKeySelfTests\.GuardSuppressionPreservesObservedHold\b') {
    throw 'Own Guard may suppress action eligibility but must not reset or re-prime any already-observed physical hold.'
}
Assert-Literals $personalStatus @(
    'var purifyClaimedPriority = purify.InputClaimed;',
    'hasPurifyRemovableCrowdControl ||',
    'var allyRescueClaimedPriority = rescue.InputClaimed;',
    'var guardianClaimedPriority = defense.InputClaimed;',
    'guardShukuchi.InputClaimed ||',
    'var jobSpecificHeldClaimedPriority = ninja.InputClaimed ||',
    'plunge.InputClaimed;',
    'var smartRecuperateClaimedPriority = recuperate.InputClaimed;',
    'miracle.InputClaimed ||',
    'var defensiveUtilityClaimedPriority = guardDefense.InputClaimed ||',
    'guardianClaimedPriority;',
    'var pressureEscapeClaimedPriority = pressureEscape.InputClaimed;',
    'configuration.EnableNinjaSeitonOnHeldGameplayKey',
    'configuration.EnableNinjaGuardShukuchiOnHeldGameplayKey',
    'ninjaGuardShukuchiHeldEnabled:',
    'ninjaSeitonHeldEnabled:',
    'ninja.InputClaimed ||',
    'scholar.InputClaimed ||',
    'emergencyInputFrame.IsConsumed'
) 'Frame-local absolute priority claims across every held helper'
if ($normalizedPersonalStatus -notmatch 'var purifyClaimedPriority = purify\.InputClaimed;.*?var ninja = ninjaSeiton\.Observe\(.*?purifyClaimedPriority \|\| emergencyInputFrame\.IsConsumed.*?var miracle = miracleIntercept\.Observe\(.*?!purifyClaimedPriority && !ninja\.InputClaimed && !emergencyInputFrame\.IsConsumed.*?var rescue = allyRescue\.Observe\(.*?dispatchAllowed: !purifyClaimedPriority && !ninja\.InputClaimed && !miracle\.InputClaimed && !emergencyInputFrame\.IsConsumed\);.*?var allyRescueClaimedPriority = rescue\.InputClaimed;.*?var defense = defensiveUtility\.ObserveGuardian\(.*?purifyClaimedPriority \|\| ninja\.InputClaimed \|\| allyRescueClaimedPriority \|\| miracle\.InputClaimed \|\| emergencyInputFrame\.IsConsumed, emergencyInputFrame.*?beginsFrame: true\)' -or
    $normalizedPersonalStatus -notmatch 'var guardShukuchi = ninjaGuardShukuchi\.Observe\(.*?guardianClaimedPriority \|\| emergencyInputFrame\.IsConsumed.*?var scholar = scholarCriticalStrategy\.Observe\(.*?guardShukuchi\.InputClaimed \|\| ninja\.InputClaimed \|\| emergencyInputFrame\.IsConsumed.*?var plunge = darkKnightPlunge\.Observe\(.*?guardShukuchi\.InputClaimed \|\| ninja\.InputClaimed \|\| scholar\.InputClaimed \|\| emergencyInputFrame\.IsConsumed' -or
    $normalizedPersonalStatus -notmatch 'var jobSpecificHeldClaimedPriority = ninja\.InputClaimed \|\| allyRescueClaimedPriority \|\| miracle\.InputClaimed \|\| guardianClaimedPriority \|\| guardShukuchi\.InputClaimed \|\| scholar\.InputClaimed \|\| plunge\.InputClaimed;.*?var recuperate = smartRecuperate\.Observe\(.*?hasPurifyRemovableCrowdControl \|\| purifyClaimedPriority \|\| jobSpecificHeldClaimedPriority \|\| emergencyInputFrame\.IsConsumed.*?var smartRecuperateClaimedPriority = recuperate\.InputClaimed;.*?var guardDefense = defensiveUtility\.ObserveGuard\(.*?purifyClaimedPriority \|\| jobSpecificHeldClaimedPriority \|\| smartRecuperateClaimedPriority \|\| emergencyInputFrame\.IsConsumed, emergencyInputFrame.*?prioritizedGuardianPass: defense\).*?var pressureEscape = pressureEscapeSprint\.Observe\(.*?purifyClaimedPriority \|\| jobSpecificHeldClaimedPriority \|\| smartRecuperateClaimedPriority \|\| defensiveUtilityClaimedPriority, emergencyInputFrame') {
    throw 'The runtime must propagate frame-local priority exactly as Purify > NIN Seiton > reactive CC > Rescue > Guardian > Guard-Shukuchi > SCH > DRK > Recuperate > Guard > Sprint, while active removable CC still absolutely blocks Recuperate.'
}
if ($normalizedPersonalStatus -notmatch 'var ninjaGuardShukuchiConfigurationEnabled = configuration\.Enabled && configuration\.EnableNinjaGuardShukuchiOnHeldGameplayKey && isCrystallineConflict && isNinja;' -or
    $normalizedPersonalStatus -notmatch 'var ninjaSeitonConfigurationEnabled = configuration\.Enabled && configuration\.EnableNinjaSeitonOnHeldGameplayKey && isCrystallineConflict && isNinja;' -or
    $normalizedPersonalStatus -match '\bsmartKardiaHeldEnabled\b' -or
    $normalizedPersonalStatus -notmatch 'var pressureEscapeClaimedPriority = pressureEscape\.InputClaimed; var kardia = smartKardia\.Observe\(.*?pressureEscapeClaimedPriority \|\| emergencyInputFrame\.IsConsumed.*?var monk = monkEarthReply\.Observe\(.*?kardia\.UseActionAttempted \|\| emergencyInputFrame\.IsConsumed') {
    throw 'Event Kardia and event Monk must remain last after all eleven physical-hold helpers, with no held-Kardia slot and no consumed-frame overtake.'
}
if ($personalStatus -match '\bstatus\.Address\b|\bStatusAddress\b') {
    throw 'Personal status scanning must never gate on status.Address.'
}
if ($normalizedPersonalStatus -notmatch 'var guardActive = DefensiveUtilityProbe\.HasActiveGuard\(localPlayer\); var exactGuardActive = guardActive; var guardObservationNow = Math\.Max\(now, Environment\.TickCount64\);.*?guardActive = defensiveUtility\.ObserveGuardSuppression\( exactGuardActive, observedGuardAttemptAt, guardObservationNow, hardReset\)\.SuppressDirectActionHelpers;') {
    throw 'Exact live or identity-and-territory-bound propagated Guard must be computed independently of the defensive-utility master before every direct-action probe.'
}
if ([regex]::Matches($personalStatus, '\bguardianCommunication\.Observe\s*\(').Count -ne 1 -or
    $normalizedPersonalStatus -notmatch 'var defense = defensiveUtility\.ObserveGuardian\(.*?beginsFrame: true\);.*?guardianCommunication\.Observe\( localPlayer, context, defense\.LastAcceptedGuardianEpisode, now, hardReset\);.*?var guardianClaimedPriority = defense\.InputClaimed;.*?var guardDefense = defensiveUtility\.ObserveGuard\(.*?prioritizedGuardianPass: defense\);.*?var defensiveUtilityClaimedPriority = guardDefense\.InputClaimed \|\| guardianClaimedPriority;') {
    throw 'Guardian must begin from a clean frame, publish its exact accepted episode to communication independently, and only then be aggregated with the later generic Guard pass.'
}
if (($personalStatus -replace '(?s)//.*?\r?\n', '') -match '\bconfiguration\.PreGuardOnLowHpPressure\b|\bEnableSageKardiaOnHeldKey\b') {
    throw 'Legacy pre-Guard and held-Kardia configuration fields may not be read by the PersonalStatus runtime.'
}
if ([regex]::Matches($pluginSource, '\bnew ReviewedPvpCommandDispatcher\s*\(').Count -ne 1 -or
    $normalizedPersonalStatus -notmatch 'GuardianCommunicationService\( configuration, clientState, objectTable, dutyState, dataManager, log, commands\)' -or
    ($pluginSource -replace '\s+', ' ') -notmatch 'var reviewedPvpCommands = new ReviewedPvpCommandDispatcher\(\); autoEnemyFocusMark = new AutoEnemyFocusMarkService\(.*?reviewedPvpCommands\);.*?personalStatus = new PersonalStatusService\(.*?reviewedPvpCommands\);') {
    throw 'Plugin startup must create one shared reviewed dispatcher and inject that same instance into Team Attack-1 and Guardian communication.'
}

# Defensive helpers share the same physical input generation. Purify owns a
# pressured Stun first; reactive Guard may follow only on a later generation
# after positive Resilience. Speculative low-HP pre-Guard is removed. PLD
# Guardian is a separate later pass with its own Job Tools master.
$defensiveUtilityRules = Read-RequiredSource $defensiveUtilityRulesPath 'Defensive utility rules'
$normalizedDefensiveUtilityRules = $defensiveUtilityRules -replace '\s+', ' '
$defensiveUtility = Read-RequiredSource $defensiveUtilityProbePath 'Defensive utility runtime'
$normalizedDefensiveUtility = $defensiveUtility -replace '\s+', ' '
Assert-Literals $defensiveUtilityRules @(
    'DefensiveUtilityFramePass(',
    'DefensiveUtilityFrameAggregation(',
    'bool GuardianOwnsPresentation,',
    'AggregateFramePasses(',
    'guardian.InputClaimed ||',
    'guardian.UseActionAttempted ||',
    '(guardian.Action == DefensiveUtilityActionKind.Guardian &&',
    '!guard.InputClaimed &&',
    '!guard.UseActionAttempted)',
    'guardian.InputClaimed || guard.InputClaimed',
    'guardian.UseActionAttempted || guard.UseActionAttempted',
    'guardian.UseActionAccepted || guard.UseActionAccepted',
    'GuardPropagationState(',
    'GuardPropagationDecision(',
    'public bool SuppressDirectActionHelpers =>',
    'ExactGuardActive || PropagationLatchActive',
    'GuardianTriggerPopup(',
    'PartySlot is >= 1 and <= 8',
    'nowMilliseconds >= StartedAtMilliseconds',
    'nowMilliseconds < EndsAtMilliseconds',
    'GuardianTriggerPopupDurationMilliseconds = 1_500',
    'ObserveGuardianTriggerPopup(',
    'hardReset || !runtimeEnabled || nowMilliseconds < 0',
    'action != DefensiveUtilityActionKind.Guardian',
    'trigger != DefensiveUtilityTrigger.PaladinGuardianLowAlly',
    '!useActionAttempted',
    '!useActionAccepted',
    'selectedPartySlot is < 1 or > 8',
    'SaturatingAdd(nowMilliseconds, GuardianTriggerPopupDurationMilliseconds)',
    'GuardPropagationLatchMilliseconds = 1_500',
    'RequiredIncomingEnemyCount = 3',
    'GuardianAllyHpPercent = 20',
    'GuardianProactiveAllyHpPercent = 35',
    'GuardianMaximumPressureAgeMilliseconds = 250',
    'PaladinGuardianRiskTier',
    'ClassifyGuardianRisk(',
    'IsFreshGuardianPressurePublication(',
    'PostPurifyGuardWindowMilliseconds = 2_000',
    'pressureKnown && incomingEnemyCount >= RequiredIncomingEnemyCount',
    '(ulong)currentHp * 100UL <= (ulong)maximumHp * (ulong)threshold',
    '!hasPurifyRemovableCrowdControl',
    '!awaitingPurifyConfirmation',
    'resilienceObserved',
    'candidate.HasNativeRangeAndLineOfSight',
    'float.IsFinite(candidate.DistanceSquared)',
    'candidate.DistanceSquared >= 0f',
    'spentActors?.Contains(candidate.Actor) == true'
) 'Exact pressure, native Guardian reachability, Resilience, and split one-intent defensive rules'
if ($normalizedDefensiveUtilityRules -notmatch 'public static DefensiveUtilityFrameAggregation AggregateFramePasses\( DefensiveUtilityFramePass guardian, DefensiveUtilityFramePass guard\) \{ var guardianOwnsPresentation = guardian\.InputClaimed \|\| guardian\.UseActionAttempted \|\| \(guardian\.Action == DefensiveUtilityActionKind\.Guardian && !guard\.InputClaimed && !guard\.UseActionAttempted\); return new DefensiveUtilityFrameAggregation\( guardianOwnsPresentation, guardian\.InputClaimed \|\| guard\.InputClaimed, guardian\.UseActionAttempted \|\| guard\.UseActionAttempted, guardian\.UseActionAccepted \|\| guard\.UseActionAccepted\); \}') {
    throw 'Defensive frame aggregation must be pure current-frame data: a Guardian claim/attempt owns presentation, unavailable Guardian stays background-only while Guard is idle, any Guard claim/attempt wins, and aggregate claim/attempt/acceptance bits are monotonic.'
}
$guardianTriggerPopupMethod = [regex]::Match(
    $normalizedDefensiveUtilityRules,
    'public static GuardianTriggerPopup\? ObserveGuardianTriggerPopup\(.*?\) \{(?<Body>.*?)\} public static bool CanDispatchPostPurifyGuard')
$guardianTriggerPopupBody = $guardianTriggerPopupMethod.Groups['Body'].Value
if (-not $guardianTriggerPopupMethod.Success -or
    [regex]::Matches($defensiveUtilityRules, 'public const long GuardianTriggerPopupDurationMilliseconds\s*=\s*1_500\s*;').Count -ne 1 -or
    $guardianTriggerPopupBody -notmatch 'if \(hardReset \|\| !runtimeEnabled \|\| nowMilliseconds < 0\) return null;.*?var current = previous is \{ \} visible && visible\.IsVisible\(nowMilliseconds\).*?if \(action != DefensiveUtilityActionKind\.Guardian \|\| trigger != DefensiveUtilityTrigger\.PaladinGuardianLowAlly \|\| !useActionAttempted \|\| !useActionAccepted \|\| selectedPartySlot is < 1 or > 8\) \{ return current; \}.*?new GuardianTriggerPopup\( selectedPartySlot, nowMilliseconds, SaturatingAdd\(nowMilliseconds, GuardianTriggerPopupDurationMilliseconds\)\).*?return next\.IsValid \? next : current;' -or
    $guardianTriggerPopupBody -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|HookFromAddress|ITargetManager|TargetManager|SetTarget|Replay|Retry|Dispatch)\b') {
    throw 'Guardian trigger popup must be a pure 1500-ms latch created only for a valid-slot attempted-and-client-accepted automatic Guardian while its runtime is enabled.'
}
if ([regex]::Matches($defensiveUtilityRules, 'public const long GuardPropagationLatchMilliseconds\s*=\s*1_500\s*;').Count -ne 1 -or
    $normalizedDefensiveUtilityRules -notmatch 'observedGuardAttemptAtMilliseconds >= 0 && observedGuardAttemptAtMilliseconds <= nowMilliseconds && observedGuardAttemptAtMilliseconds > lastObservedAttempt\) \{ lastObservedAttempt = observedGuardAttemptAtMilliseconds; expiresAt = SaturatingAdd\( observedGuardAttemptAtMilliseconds, GuardPropagationLatchMilliseconds\); \}' -or
    $normalizedDefensiveUtilityRules -notmatch 'if \(exactGuardActive\) expiresAt = -1; var latchActive = !exactGuardActive && expiresAt > nowMilliseconds; var next = new GuardPropagationState\( lastObservedAttempt, latchActive \? expiresAt : -1\);' -or
    $normalizedDefensiveUtilityRules -match 'observedGuardAttemptAtMilliseconds\s*>=\s*lastObservedAttempt') {
    throw 'Guard propagation must last exactly 1500ms from each first/new attempt timestamp, reject duplicate re-extension, and retire its latch on exact Guard membership.'
}
if ($defensiveUtilityRules -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|Replay|Retry|Dispatch)\b') {
    throw 'Pure defensive utility rules may only decide eligibility and visual latch state; they must never call, replay, suppress, or retarget an action.'
}
$guardianCandidateMethod = [regex]::Match(
    $normalizedDefensiveUtilityRules,
    'public static bool IsGuardianCandidate\(.*?\) \{.*?\} .*?public static PaladinGuardianRiskTier ClassifyGuardianRisk').Value
$guardianRiskMethod = [regex]::Match(
    $normalizedDefensiveUtilityRules,
    'public static PaladinGuardianRiskTier ClassifyGuardianRisk\(.*?\) \{(?<Body>.*?)\} public static bool IsFreshGuardianPressurePublication')
$guardianRiskBody = $guardianRiskMethod.Groups['Body'].Value
$guardianPressureFreshnessMethod = [regex]::Match(
    $normalizedDefensiveUtilityRules,
    'public static bool IsFreshGuardianPressurePublication\( long nowMilliseconds, long publishedAtMilliseconds\) => (?<Body>.*?); public static int SelectGuardianCandidateIndex')
$guardianPressureFreshnessBody = $guardianPressureFreshnessMethod.Groups['Body'].Value
if ($normalizedDefensiveUtilityRules -notmatch 'CanDispatchPostPurifyGuard\(.*?\) => !awaitingPurifyConfirmation && resilienceObserved && !hasPurifyRemovableCrowdControl && nowMilliseconds >= 0 && expiresAtMilliseconds > nowMilliseconds' -or
    [string]::IsNullOrWhiteSpace($guardianCandidateMethod) -or
    $guardianCandidateMethod -notmatch 'candidate\.HasValidNativeTarget && candidate\.HasNativeRangeAndLineOfSight && float\.IsFinite\(candidate\.DistanceSquared\) && candidate\.DistanceSquared >= 0f && ClassifyGuardianRisk\(candidate\) != PaladinGuardianRiskTier\.None' -or
    -not $guardianRiskMethod.Success -or
    $guardianRiskBody -notmatch 'IsAtOrBelowHpPercent\( candidate\.CurrentHp, candidate\.MaximumHp, GuardianAllyHpPercent\).*?return PaladinGuardianRiskTier\.Critical;.*?IsTrustedIncomingEnemyCount\(candidate\.IncomingEnemyCount\) && candidate\.IncomingEnemyCount >= RequiredIncomingEnemyCount && IsAtOrBelowHpPercent\( candidate\.CurrentHp, candidate\.MaximumHp, GuardianProactiveAllyHpPercent\).*?PaladinGuardianRiskTier\.ProactiveHighPressure.*?PaladinGuardianRiskTier\.None' -or
    -not $guardianPressureFreshnessMethod.Success -or
    $guardianPressureFreshnessBody -notmatch 'nowMilliseconds >= 0 && publishedAtMilliseconds >= 0 && publishedAtMilliseconds <= nowMilliseconds && nowMilliseconds - publishedAtMilliseconds <= GuardianMaximumPressureAgeMilliseconds' -or
    $guardianCandidateMethod -match 'DistanceSquared\s*<' -or
    $defensiveUtilityRules -match '\bGuardianStrictMaximumDistance\b|\bstrictMaximumDistanceSquared\b') {
    throw 'Defensive rules must retain unconditional Guardian at <=20%, allow <=35% only from exact trusted 3+ pressure, and require finite native range/LoS.'
}
if ([regex]::Matches($defensiveUtilityRules, '\bReservedRemovedPreGuard\b').Count -ne 1 -or
    $defensiveUtility -match '\bPreGuard\w*\b|\bpreGuard\w*\b') {
    throw 'The only surviving pre-Guard token may be the reserved enum value needed for diagnostic compatibility; no rule or runtime path may remain.'
}
if ([regex]::Matches($defensiveUtility, '(?:->|\.)UseAction\s*\(').Count -ne 2) {
    throw 'Defensive utility runtime must contain exactly one Guard and one Guardian native UseAction boundary.'
}
Assert-Literals $defensiveUtility @(
    'EnemyCombatConstants.GuardActionId',
    'EnemyCombatConstants.GuardianActionId',
    'purifyUseActionAttempted',
    'awaitingPostPurifyConfirmation = true',
    'resilienceActive &&',
    '!hasPurifyRemovableCrowdControl',
    'private FrozenGuardRetry? frozenGuardRetry;',
    'private FrozenGuardianRetry? frozenGuardianRetry;',
    'private VirtualKey terminalGuardKey = VirtualKey.NO_KEY;',
    'private VirtualKey terminalGuardianKey = VirtualKey.NO_KEY;',
    'HeldActionRetryRules.RetainsSchedulerFrame(',
    'HeldActionRetryRules.CanAttemptFrozenIntent(',
    'HeldActionRetryRules.Complete(',
    'HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(',
    'inputFrame.Consume()',
    'nearAssist.RunWithoutRedirect',
    'ClientActionAttemptBoundary.Capture(',
    'ClientActionAttemptBoundaryRules.Classify(',
    'ActionManager.UseActionMode.None',
    'PartySlotResolver.Resolve',
    'GetActionInRangeOrLoS',
    'TryCaptureIncomingAllyPressure(',
    'IsFreshGuardianPressurePublication(',
    'SelectGuardianCandidateIndex',
    'GuardianTriggerPopup? GuardianPopup',
    'selectedGuardianPartySlot = selected.PartySlot',
    'ObserveGuardianTriggerPopup(',
    'HasActiveGuard(localPlayer)',
    'CaptureLocalGuardAttemptGeneration()',
    'nearAssist.CanProtectAutomaticGuard',
    'if (outcome == ClientActionAttemptOutcome.ClientRejected)',
    'TryRetractClientRejectedLocalGuardAttempt(',
    'if (outcome == ClientActionAttemptOutcome.ClientAccepted)',
    'TryArmAcceptedAutoGuardProtection(',
    'accepted automatic Guard but could not prove exact protection ownership.',
    'ObserveGuardSuppression('
) 'Shared retry exact Guard and Guardian runtime'
if ($normalizedDefensiveUtility -notmatch 'var prior = beginsFrame \? CreateFrameSnapshotBase\(guardActive\) : Snapshot;' -or
    $normalizedDefensiveUtility -notmatch 'private DefensiveUtilityProbeSnapshot CreateFrameSnapshotBase\(bool guardActive\) => DefensiveUtilityProbeSnapshot\.Initial with \{ Active = false, GuardActive = guardActive,.*?LastEvent = "Frame initialized", \};' -or
    $normalizedDefensiveUtility -notmatch 'var guardResult = new DefensiveUtilityProbeSnapshot\(.*?var result = prioritizedGuardianPass is \{ \} guardianPass \? MergePrioritizedGuardianPass\(guardResult, guardianPass\) : guardResult;' -or
    $normalizedDefensiveUtility -notmatch 'private static DefensiveUtilityProbeSnapshot MergePrioritizedGuardianPass\(.*?DefensiveUtilityRules\.AggregateFramePasses\(.*?Action = aggregate\.GuardianOwnsPresentation \? guardianPass\.Action : guardPass\.Action,.*?InputClaimed = aggregate\.InputClaimed,.*?UseActionAttempted = aggregate\.UseActionAttempted, UseActionAccepted = aggregate\.UseActionAccepted,.*?LastEvent = aggregate\.GuardianOwnsPresentation \? guardianPass\.LastEvent : guardPass\.LastEvent,') {
    throw 'Guardian must start from a clean frame-local snapshot, and the later Guard pass must publish only their current-frame aggregate without stale or masked Guard diagnostics.'
}
$defensiveUtilitySelfTests = Read-RequiredSource $defensiveUtilitySelfTestsPath 'Defensive utility self-tests'
Assert-Literals $defensiveUtilitySelfTests @(
    'public static void IndependentGuardianAndGuardPassesAggregateCurrentFrameOnly()',
    'Guardian cast/throttle wait stays visible',
    'Guardian claim survives later Guard pass',
    'idle Guardian cannot mask current Guard',
    'unready Guardian candidate cannot mask the Guard which actually acted',
    'later Guard acceptance remains visible',
    'unready Guardian remains visible while the later Guard pass is idle',
    'background Guardian diagnostics cannot synthesize a claim',
    'background Guardian diagnostics cannot synthesize an attempt',
    'no stale prior-frame owner is synthesized',
    'no stale prior-frame claim is synthesized',
    'no stale prior-frame attempt is synthesized'
) 'Independent current-frame Guardian/Guard aggregation regression'
Assert-Literals $defensiveUtilitySelfTests @(
    'public static void GuardianProactiveRiskRequiresExactHighPressure()',
    'public static void GuardianPressurePublicationFreshnessIsBounded()',
    'exactly 35 percent with exact 3+ pressure enters the proactive tier',
    'unknown or stale pressure does not raise the legacy threshold',
    'the exact 250-ms pressure-age boundary is inclusive',
    'pressure older than 250 ms cannot raise the legacy threshold',
    'the original 20-percent boundary stays unconditional',
    'the unconditional critical tier always precedes proactive pressure',
    'inside the proactive tier pressure wins first, then exact HP'
) 'Pressure-aware Guardian rescue tiers'
Assert-Literals $coreSelfTestProgramForGuardian @(
    'DefensiveUtilitySelfTests.IndependentGuardianAndGuardPassesAggregateCurrentFrameOnly',
    'DefensiveUtilitySelfTests.GuardianProactiveRiskRequiresExactHighPressure',
    'DefensiveUtilitySelfTests.GuardianPressurePublicationFreshnessIsBounded'
) 'Independent Guardian/Guard aggregation test registration'
if ([regex]::Matches($defensiveUtility, '\bClientActionAttemptBoundary\.Capture\s*\(').Count -ne 4 -or
    [regex]::Matches($defensiveUtility, '\bClientActionAttemptBoundaryRules\.Classify\s*\(').Count -ne 2 -or
    [regex]::Matches($defensiveUtility, '\bHeldActionRetryRules\.Complete\s*\(').Count -ne 2 -or
    [regex]::Matches($defensiveUtility, '\bHeldActionRetryRules\.RetainsSchedulerFrame\s*\(').Count -ne 2 -or
    $normalizedDefensiveUtility -notmatch 'var generationBeforeCall = nearAssist\.CaptureLocalGuardAttemptGeneration\(\);.*?var outcome = ClientActionAttemptBoundaryRules\.Classify\(.*?if \(outcome == ClientActionAttemptOutcome\.ClientRejected\) \{ nearAssist\.TryRetractClientRejectedLocalGuardAttempt\( localPlayer\.GameObjectId, localPlayer\.EntityId, generationBeforeCall\); \}.*?if \(outcome == ClientActionAttemptOutcome\.ClientAccepted\) \{ if \(!nearAssist\.TryArmAcceptedAutoGuardProtection\( localPlayer\.GameObjectId, localPlayer\.EntityId, generationBeforeCall\)\).*?log\.Warning\(.*?var acceptedAt = Environment\.TickCount64; ObserveGuardSuppression\(' -or
    $normalizedDefensiveUtility -match 'AcceptanceUnknown.*?TryRetractClientRejectedLocalGuardAttempt') {
    throw 'Guard and Guardian must use the shared classifier; only a proven clean client false rolls back the exact Guard generation, while accepted Auto-Guard must attempt exact cancellation ownership before propagation.'
}
$autoGuardProtectionRules = Read-RequiredSource $autoGuardProtectionRulesPath 'Auto-Guard cancellation protection rules'
$normalizedAutoGuardProtectionRules = $autoGuardProtectionRules -replace '\s+', ' '
Assert-Literals $autoGuardProtectionRules @(
    'StatusPropagationMilliseconds = 1_500',
    'MaximumOwnedDurationMilliseconds = 6_000',
    'generationBeforeCall == long.MaxValue',
    'latestGuardAttemptGeneration == expectedGeneration',
    'AutoGuardProtectionDecisionReason.ExplicitGuardReuse',
    'AutoGuardProtectionDecisionReason.GuardEnded',
    'AutoGuardProtectionDecisionReason.PropagationExpired',
    'AutoGuardProtectionDecisionReason.MaximumDurationReached'
) 'Exact accepted-attempt Auto-Guard ownership and bounded fail-open lifecycle'
if ($autoGuardProtectionRules -match '\b(?:UseAction|UseActionLocation|ActionManager|IPlayerCharacter|HookFromAddress|ITargetManager|TargetManager|SetTarget|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b' -or
    $normalizedAutoGuardProtectionRules -notmatch 'if \(!previous\.IsArmed\).*?if \(observation\.HardReset\).*?if \(observation\.NowMilliseconds < 0 \|\| observation\.NowMilliseconds < previous\.AcceptedAtMilliseconds\).*?if \(!observation\.RuntimeEnabled\).*?if \(observation\.TerritoryId != previous\.TerritoryId\).*?if \(!observation\.LocalPlayerLive\).*?if \(observation\.LocalPlayer != previous\.LocalPlayer\).*?if \(observation\.NowMilliseconds >= previous\.MaximumExpiresAtMilliseconds\).*?if \(observation\.IsExplicitGuardReuse\).*?var exactGuardObserved = previous\.ExactGuardObserved \|\| observation\.ExactGuardActive;.*?if \(previous\.ExactGuardObserved && !observation\.ExactGuardActive\).*?StatusPropagationMilliseconds.*?observation\.ActionCanCancelGuard') {
    throw 'Auto-Guard Core must remain pure, release on reset/clock/runtime/context/player/status/reuse/timeout boundaries, bridge exactly 1.5 seconds, and block only a classified cancelling action.'
}
$autoGuardProtectionTestMethods = @(
    'AutoGuardProtectionOwnershipRequiresTheExactAcceptedAttempt',
    'AutoGuardProtectionBridgesPropagationAndFollowsTheExactStatus',
    'AutoGuardProtectionHasExplicitAndBoundedReleasePaths',
    'AutoGuardProtectionContextDriftAlwaysFailsOpen'
)
foreach ($method in $autoGuardProtectionTestMethods) {
    Assert-Literals $defensiveUtilitySelfTests @("public static void $method()") "Auto-Guard self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("DefensiveUtilitySelfTests.$method") "Auto-Guard registration $method"
}
$nearAssistAutoGuard = Read-RequiredSource $nearAssistPath 'Auto-Guard central native boundaries'
$normalizedNearAssistAutoGuard = $nearAssistAutoGuard -replace '\s+', ' '
Assert-Literals $nearAssistAutoGuard @(
    '[ThreadStatic]',
    'explicitAutoGuardBreakBypassDepth',
    'useActionHook?.IsEnabled == true',
    'useActionLocationHook?.IsEnabled == true',
    'TryGetActionMetadata(',
    'action.IsPvP',
    'ClearAutoGuardProtection("Action classification failed open")',
    'ClearAutoGuardProtection("Released: explicit /panicshu override")'
) 'Dual-hook automatic Guard cancellation protector'
if ($normalizedDefensiveUtility -notmatch 'var guardActionSpecificallyReady = guardMetadataVerified && nearAssist\.CanProtectAutomaticGuard && IsActionSpecificallyReady' -or
    $normalizedDefensiveUtility -notmatch 'private unsafe ClientActionAttemptOutcome TryUseGuardOnce\(.*?if \(!guardMetadataVerified \|\| !nearAssist\.CanProtectAutomaticGuard') {
    throw 'Automatic Guard must fail closed before dispatch unless both central protection hooks are enabled.'
}
if ($normalizedNearAssistAutoGuard -notmatch 'internal bool CanProtectAutomaticGuard => !disposed && started && useActionHook\?\.IsEnabled == true && useActionLocationHook\?\.IsEnabled == true;' -or
    $normalizedNearAssistAutoGuard -notmatch 'private bool UseActionDetour\(.*?if \(TryBlockOwnedAutoGuardCancellation\(thisPtr, actionType, actionId\)\) return false; var forwardedTargetId' -or
    $normalizedNearAssistAutoGuard -notmatch 'private bool UseActionLocationDetour\(.*?if \(explicitAutoGuardBreakBypassDepth > 0\).*?ClearAutoGuardProtection\("Released: explicit /panicshu override"\);.*?else if \(TryBlockOwnedAutoGuardCancellation\(thisPtr, actionType, actionId\)\).*?return false;.*?return useActionLocationHook!\.Original' -or
    $normalizedNearAssistAutoGuard -notmatch 'private static bool IsSupportedActionType\(ActionType actionType\) => actionType is ActionType\.Action or ActionType\.PvPAction;' -or
    $normalizedNearAssistAutoGuard -notmatch 'var explicitGuardReuse = supportedActionType && \(actionId == EnemyCombatConstants\.GuardActionId \|\| resolvedActionId == EnemyCombatConstants\.GuardActionId\);.*?var actionCanCancelGuard = supportedActionType && resolvedActionId != 0 && TryGetActionMetadata\(.*?action\.IsPvP && !explicitGuardReuse;.*?ApplyAutoGuardProtectionObservation') {
    throw 'Both central action boundaries must protect owned Auto-Guard before redirect tokens, allow raw/resolved Guard reuse, cover location calls, and fail open for unknown or non-PvP actions.'
}
$runWithoutRedirectRegion = [regex]::Match(
    $normalizedNearAssistAutoGuard,
    'internal T RunWithoutRedirect<T>\(.*?internal IDisposable EnterExplicitAutoGuardBreak\(')
if ([regex]::Matches(($nearAssistAutoGuard + $defensiveUtility), '\bTryArmAcceptedAutoGuardProtection\s*\(').Count -ne 2 -or
    $normalizedNearAssistAutoGuard -notmatch 'internal IDisposable EnterExplicitAutoGuardBreak\(\).*?explicitAutoGuardBreakBypassDepth\+\+;.*?new ExplicitAutoGuardBreakScope' -or
    !$runWithoutRedirectRegion.Success -or
    $runWithoutRedirectRegion.Value -match 'explicitAutoGuardBreakBypassDepth') {
    throw 'Only the exact accepted automatic Guard call may arm ownership, and only the dedicated scoped /panicshu override may release location dispatch outside the protector.'
}
if ($normalizedDefensiveUtility -notmatch 'actionManager->UseAction\( ActionType\.Action, EnemyCombatConstants\.GuardActionId, localPlayer\.GameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)' -or
    $normalizedDefensiveUtility -notmatch 'actionManager->UseAction\( ActionType\.Action, EnemyCombatConstants\.GuardianActionId, ally\.GameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)') {
    throw 'Defensive native calls must be exact Action 29054 to self and exact Action 29066 to the revalidated ally.'
}
if ($normalizedDefensiveUtility -notmatch 'var canDispatch = guardConfigurationEnabled && isCrystallineConflict && localIdentityValid && input\.ProbeSucceeded && !input\.IsTextInputActive && inputEligible && !guardActive && !higherPriorityClaimed;' -or
    $normalizedDefensiveUtility -notmatch 'var canDispatch = guardianConfigurationEnabled && isCrystallineConflict && localIdentityValid && input\.ProbeSucceeded && !input\.IsTextInputActive && inputEligible && !guardActive && !higherPriorityClaimed;') {
    throw 'Reactive Guard and independent PLD Guardian must each retain their own config gate while both honor the effective live-or-propagated Guard suppression and shared priority.'
}
if ($defensiveUtility -match '(?-i:\b(RetryAction|RetryDispatch|QueueAction)\b)|(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=(?!=|>)|\bstatus\.Address\b') {
    throw 'Defensive utilities must never own a custom action queue, mutate a target, or depend on status-slot addresses.'
}

$allyRescue = Read-RequiredSource $allyRescueProbePath 'Ally Rescue probe'
$normalizedAllyRescue = $allyRescue -replace '\s+', ' '
if ([regex]::Matches($allyRescue, '\bUseAction\s*\(').Count -ne 1) {
    throw 'Ally Rescue must contain exactly one native UseAction call.'
}
Assert-Literals $allyRescue @(
    'WardensPaeanActionId = 29400',
    'AquaveilActionId = 29227',
    'WardensPaeanIconId = 9628',
    'AquaveilIconId = 9607',
    'BardJobId = 23',
    'WhiteMageJobId = 24',
    'ExpectedRange = 30',
    'WardensPaeanRecast100ms = 240',
    'AquaveilRecast100ms = 180',
    'ValidateRescueActionMetadata',
    "The Warden's Paean",
    'Aquaveil',
    '"Removes"',
    '"Nullifies"',
    'StringComparison.OrdinalIgnoreCase',
    'description.Contains(expectedCleanseVerb, StringComparison.OrdinalIgnoreCase)',
    'description.Contains("status affliction", StringComparison.OrdinalIgnoreCase)',
    'description.Contains("Purify", StringComparison.OrdinalIgnoreCase)',
    'AllyRescueBufferRules.Observe',
    'AllyRescueStatusRules.IsTriggerStatus',
    'PartySlotResolver.Resolve',
    'pressureTracker.TryGetIncomingAllyPressure',
    'GetActionInRangeOrLoS',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight',
    'state = decision.NextState',
    'var inputClaimed = dispatchAllowed &&',
    'if (inputClaimed) inputFrame.Consume()',
    'TryRevalidateCandidate',
    'AllyRescueBufferRules.CompleteNativeAttempt(',
    'ClientActionAttemptBoundary.Capture(',
    'ClientActionAttemptBoundaryRules.Classify(',
    'nearAssist.RunWithoutRedirect',
    'ActionType.Action',
    'ActionManager.UseActionMode.None'
) 'Bounded Ally Rescue runtime'
if ($allyRescue -match '\bstatus\.Address\b') {
    throw 'Ally Rescue must never depend on a native status-slot address.'
}
if ([regex]::Matches($allyRescue, 'ValidateRescueActionMetadata\s*\(').Count -lt 3 -or
    $allyRescue -notmatch 'catch\s*\(Exception exception\)' -or
    $allyRescue -notmatch 'metadata lookup failed closed') {
    throw 'Each Ally Rescue action must validate current English metadata independently and fail closed on lookup errors.'
}
if ($normalizedAllyRescue -notmatch 'string\.Equals\( action\.Name\.ToString\(\), expectedName, StringComparison\.OrdinalIgnoreCase\)' -or
    $normalizedAllyRescue -notmatch 'description\.Contains\(expectedCleanseVerb, StringComparison\.OrdinalIgnoreCase\)' -or
    $normalizedAllyRescue -notmatch 'description\.Contains\("status affliction", StringComparison\.OrdinalIgnoreCase\)' -or
    $normalizedAllyRescue -notmatch 'description\.Contains\("Purify", StringComparison\.OrdinalIgnoreCase\)') {
    throw 'Ally Rescue action metadata must use case-insensitive names and stable cleanse-description tokens.'
}

$allyRescueConfirmationRules = Read-RequiredSource $allyRescueConfirmationRulesPath 'Ally Rescue confirmation rules'
$normalizedAllyRescueConfirmationRules = $allyRescueConfirmationRules -replace '\s+', ' '
Assert-Literals $allyRescueConfirmationRules @(
    'WardensPaeanActionId = 29400',
    'AquaveilActionId = 29227',
    'RecoveredFromStatusEffectType = 0x10',
    'StunStatusId = 1343',
    'HeavyStatusId = 1344',
    'BindStatusId = 1345',
    'SilenceStatusId = 1347',
    'MiracleOfNatureStatusId = 3085',
    'DeepFreezeStatusId = 3219',
    'actionId is WardensPaeanActionId or AquaveilActionId',
    'observation.EffectType == RecoveredFromStatusEffectType',
    'UseActionAccepted &&',
    'ExpectedSourceSequence != 0',
    'observation.SourceSequence == pending.ExpectedSourceSequence',
    'MaximumConfirmedKeys = 128'
) 'Exact source-sequence-owned Ally Rescue confirmation correlation'
if ($normalizedAllyRescueConfirmationRules -notmatch 'IsConfirmableRemovedStatus\(uint statusId\) => statusId is StunStatusId or HeavyStatusId or BindStatusId or SilenceStatusId or MiracleOfNatureStatusId or DeepFreezeStatusId;' -or
    $normalizedAllyRescueConfirmationRules -match 'IsConfirmableRemovedStatus\(uint statusId\) =>[^;]*(?:134[0-9]|30[0-9]{2}|32[0-9]{2})' -or
    $normalizedAllyRescueConfirmationRules -notmatch 'Intent\.EntityId == TargetEntityId && UseActionAccepted && ExpectedSourceSequence != 0 && AttemptedAtMilliseconds >= 0' -or
    $normalizedAllyRescueConfirmationRules -notmatch 'observation\.EffectType == RecoveredFromStatusEffectType && IsConfirmableRemovedStatus\(observation\.EffectValue\) && observation\.SourceSequence == pending\.ExpectedSourceSequence') {
    throw 'Ally Rescue confirmation must accept exactly the six reviewed removable-status constants.'
}
if ($allyRescueConfirmationRules -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|RetryAction|RetryDispatch|ITargetManager|TargetManager|SetTarget)\b') {
    throw 'Pure Ally Rescue confirmation rules must never initiate actions, retry, or access target mutation APIs.'
}
$allyRescueConfirmationSelfTests = Read-RequiredSource (
    Join-Path $coreSelfTestRoot 'AllyRescueConfirmationSelfTests.cs') 'Ally Rescue confirmation self-tests'
Assert-Literals $allyRescueConfirmationSelfTests @(
    'accepted call without exact source sequence cannot claim automation',
    'manual same-action same-target packet cannot confirm helper ownership',
    'pending retains exact helper source sequence',
    'later exact helper source sequence still confirms'
) 'Ally Rescue manual-action attribution regression coverage'

$metadataGuard = Read-RequiredSource (Join-Path $pluginServicesRoot 'SeitonMetadataGuard.cs') 'PvP metadata guard'
Assert-Literals $metadataGuard @(
    'AllyRescueStatusesVerified',
    'ValidateFeature("Ally Rescue statuses"',
    'EnemyCombatConstants.PvPStunStatusId',
    'EnemyCombatConstants.PvPSilenceStatusId',
    'EnemyCombatConstants.DeepFreezeStatusId',
    'EnemyCombatConstants.MiracleOfNatureStatusId'
) 'Independent Ally Rescue status metadata'
if ($normalizedAllyRescue -notmatch 'actionManager->UseAction\s*\(\s*ActionType\.Action\s*,\s*actionId\s*,\s*targetGameObjectId\s*,\s*0\s*,\s*ActionManager\.UseActionMode\.None\s*,\s*0\s*\)') {
    throw 'Ally Rescue must issue only the selected verified action to the exact selected ally via ActionType.Action and UseActionMode.None.'
}
$rescueCommit = [regex]::Match($allyRescue, 'state\s*=\s*decision\.NextState\s*;')
$rescueCall = [regex]::Match($allyRescue, 'actionManager->UseAction\s*\(')
if (-not $rescueCommit.Success -or -not $rescueCall.Success -or $rescueCommit.Index -gt $rescueCall.Index) {
    throw 'Ally Rescue must commit its frozen buffer state before the native action boundary.'
}
if ($allyRescue -match '\b(for|while|do)\s*\([^)]*UseAction' -or
    $allyRescue -match '\b(RetryAction|RetryDispatch|QueueAction|ITargetManager|TargetManager|SetTarget)\b') {
    throw 'Ally Rescue must never own a custom queue, loop native calls inside one frame, or mutate visible targets.'
}
if ($normalizedAllyRescue -notmatch 'var outcome = ClientActionAttemptOutcome\.NotInvoked; ushort expectedSourceSequence = 0;.*?TryUseRescueOnce\( localPlayer!, actionId, revalidated\.GameObjectId, out attempted, out expectedSourceSequence\).*?if \(attempted && accepted && expectedSourceSequence != 0\).*?new AllyRescuePendingAttempt\( localPlayer!\.EntityId, actionId, revalidated\.GameObjectId, revalidated\.EntityId, dispatchIntent, accepted, attemptedAt, expectedSourceSequence\)' -or
    $normalizedAllyRescue -notmatch 'var boundaryBefore = ClientActionAttemptBoundary\.Capture\(actionManager, actionId\);.*?var accepted = nearAssist\.RunWithoutRedirect.*?var boundaryAfter = ClientActionAttemptBoundary\.Capture\(actionManager, actionId\); if \(accepted && boundaryAfter\.LastUsedActionSequence != 0 && boundaryAfter\.LastUsedActionSequence != boundaryBefore\.LastUsedActionSequence\).*?expectedSourceSequence = boundaryAfter\.LastUsedActionSequence') {
    throw 'Ally Rescue landing confirmation must register only a client-accepted exact boundary whose non-zero LastUsedActionSequence advanced, and correlate only that same source sequence so a manual Paean/Aquaveil cannot claim AUTO.'
}

$allyRescueSelection = Read-RequiredSource (Join-Path $coreRoot 'AllyRescueSelectionRules.cs') 'Ally Rescue selection rules'
Assert-Literals $allyRescueSelection @(
    'StunStatusId = 1343',
    'SilenceStatusId = 1347',
    'MiracleOfNatureStatusId = 3085',
    'DeepFreezeStatusId = 3219',
    'candidate.CurrentHp * current.MaximumHp',
    'ComparePressure',
    'CompareMp',
    'candidate.DistanceSquared.CompareTo(current.DistanceSquared)',
    'candidate.IsExactPartyMember',
    '!candidate.IsSelf',
    'candidate.IsAlive',
    'candidate.IsTargetable',
    'candidate.HasNativeRangeAndLineOfSight'
) 'Exact Ally Rescue trigger and priority rules'
if ($allyRescueSelection -match '\b(HeavyStatusId|BindStatusId)\b|\b1344\b|\b1345\b') {
    throw 'Heavy and Bind must remain excluded from Ally Rescue triggers.'
}

# Reactive CC is the highest-priority job-specific direct-action boundary,
# immediately after Purify and before Ally Rescue, Guardian, NIN, SCH, DRK,
# Smart Recuperate, generic Guard, and pressure Sprint. Startup events retain
# one-generation ownership; protection-end events retain only exact held consent.
$miracleIntercept = Read-RequiredSource $miracleInterceptProbePath 'Miracle intercept probe'
$normalizedMiracleIntercept = $miracleIntercept -replace '\s+', ' '
if ([regex]::Matches($miracleIntercept, '(?:->|\.)UseAction\s*\(').Count -ne 1) {
    throw 'Reactive CC must contain exactly one native UseAction call shared by WHM, BRD, and NIN.'
}
Assert-Literals $miracleIntercept @(
    'MiracleInterceptRules.GetThreatLifetimeMilliseconds(kind)',
    'RequiredMiracleProtectionStatusIds',
    'RequiredSilentProtectionStatusIds',
    'CcImmunityBrakeActionCatalog',
    'GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.Miracle)',
    'GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.StandardPurifyCc)',
    '.Append(EnemyCombatConstants.HardenedScalesStatusId)',
    '.Distinct()',
    'EnemyCombatConstants.HardenedScalesStatusId',
    'RequiredProtectionStatusIds(counterActionId).All(',
    'verifiedProtectionStatusIds.Contains',
    'isCrystallineConflict',
    'EnemyCombatConstants.WhiteMageJobId',
    'EnemyCombatConstants.BardJobId',
    'EnemyCombatConstants.NinjaJobId',
    'EnemyCombatConstants.DancerJobId',
    'EnemyCombatConstants.MiracleOfNatureActionId',
    'EnemyCombatConstants.SilentNocturneActionId',
    'EnemyCombatConstants.NinjaAeolianEdgeComboCarrierActionId',
    'EnemyCombatConstants.ForkedRaijuActionId',
    'EnemyCombatConstants.FleetingRaijuActionId',
    'EnemyCombatConstants.ContradanceActionId',
    'signal.LocalEntityId != localPlayer.EntityId',
    'EnemyCombatConstants.MachinistJobId',
    'EnemyCombatConstants.SamuraiJobId',
    'EnemyCombatConstants.ViperJobId',
    'executeTracker.Enemies',
    'HasAnyVerifiedCcProtection',
    'HasVerifiedActiveStatus',
    'CcImmunityBrakeActionCatalog.IsBlockerStatus(',
    'BlockerFamilyForAction(threat.CounterActionId)',
    'Actor status-list membership is the authoritative live presence',
    'GetActionInRangeOrLoS',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight',
    'MaximumTeamPressureAgeMilliseconds = 250',
    'TryGetFreshTeamTargetCount(',
    'pressureTracker.TryGetFreshTeamTargetCount(',
    'activeThreat = null',
    'inputFrame.Consume()',
    'TryUseCounterCcOnce(',
    'nearAssist.RunWithoutRedirect',
    'ActionType.Action',
    'ActionManager.UseActionMode.None',
    'the action will not be retried',
    'internal long RecognizedThreatCount { get; init; }',
    'internal long ArmedThreatCount { get; init; }',
    'internal long RejectedThreatCount { get; init; }',
    'internal long PriorityWaitCount { get; init; }',
    'internal long NoInputWaitCount { get; init; }',
    'internal long RangeWaitCount { get; init; }',
    'internal long ProtectionWaitCount { get; init; }',
    'internal long ExpiredThreatCount { get; init; }',
    'internal string LastOpportunity { get; init; } = "None observed"',
    'WithOpportunityDiagnostics(',
    'RecordWait(threat, MiracleWaitReason.HigherPriorityHelper)',
    'RecordExpired(expiringThreat)',
    'ResetWaitDiagnostics()',
    'bool enableContradance',
    'bool enablePostPurifyCrowdControl',
    'bool enablePostGuardCrowdControl',
    'bool purifyMetadataVerified',
    'var cleanseFollowupEnabled = enabled &&',
    'var guardFollowupEnabled = enabled &&',
    'MiracleGuardFollowupRules.GuardStatusId',
    'MiracleGuardFollowupRules.GuardStatusAlternateId',
    'capture.SetMiracleCleanseFollowupLocalEntityId(',
    'MiracleCleanseFollowupRules.ResilienceAcquisitionMilliseconds',
    'signal.FeatureGeneration != capture.CurrentMiracleCleanseFollowupGeneration',
    'Dictionary<int, MiracleCleanseFollowupState> cleanseFollowupStates = []',
    'List<MiracleCleanseFollowupPendingResolution>',
    'pendingCleanseTargetResolutions = []',
    'ResolvePendingCleanseTargets(',
    'ResolveCleanseTarget(',
    'ResolveUniqueCanonicalCleanseEnemy(',
    'MiracleCleanseFollowupRules.MaximumPendingResolutions',
    'cleanseFollowupStates.Keys.Order().ToArray()',
    'EnemySlotRules.IsValidSlot(enemySlot)',
    'ClearCleanseFollowupStates()',
    'ResolveCleanseFollowupCandidate(',
    'CountActiveStatuses(',
    'input.FreshGameplayKeyPressed',
    'input.HeldGameplayKeyEligible',
    'inputFrame.IsGameplayKeyPhysicallyDown(',
    'inputFrame.IsGameplayKeyGenerationEligible(',
    'ObserveProtectionEndHeldConsent(',
    'MiracleProtectionEndRules.ObserveHeldConsent(',
    'protectionEndJobChanged',
    'protectionEndLocalJobId',
    'ClearProtectionEndHeldConsent()',
    'MiracleCleanseFollowupRules.Observe(',
    'cleanseFollowupStates[enemySlot] = decision.NextState',
    'decision.ShouldPromote',
    'decision.PromotionIntent',
    'MiracleInterceptThreatKind.PostPurifyCrowdControl',
    'MiracleInterceptThreatKind.PostGuardCrowdControl',
    'MiracleProtectionEndRules.HeldLeaseMilliseconds',
    'ObserveGuardFollowup(',
    'BuildGuardFollowupCandidates(',
    'CountActiveGuardStatuses(',
    'int EnemySlot',
    'EnemySlotRules.IsValidSlot(threat.EnemySlot)',
    'enemy.Slot == threat.EnemySlot',
    'var guardReappeared =',
    'var revalidatedGuardAbsent =',
    'MiracleGuardFollowupRules.Observe(',
    'SelectFollowupPromotion(',
    'CompareFollowupPromotions(',
    'MiracleProtectionEndRules.Compare(left.Rank, right.Rank)',
    'MiracleProtectionEndRankCandidate',
    'canonical.HasTrustedMp',
    'CounterActionId',
    'LocalJobId',
    'foreach (var retired in followupPromotions)',
    'GuardFollowupTrackedCount',
    'GuardFollowupReleaseReadyCount',
    'GuardFollowupTargetGameObjectId',
    'GuardFollowupTargetEntityId',
    'GuardFollowupTeamPressure',
    'GuardFollowupEpisodeCount',
    'GuardFollowupPromotionCount',
    'GuardFollowupExpiredCount',
    'GuardFollowupRetiredCount',
    'GuardFollowupLastEvent',
    'ProtectionEndHeldConsentActive',
    'ProtectionEndHeldConsentKey',
    'ProtectionEndRankTeamPressureKnown',
    'ProtectionEndRankTeamPressure',
    'ProtectionEndRankCurrentHp',
    'ProtectionEndRankMaximumHp',
    'ProtectionEndRankMpKnown',
    'ProtectionEndRankCurrentMp',
    'ProtectionEndRankMaximumMp'
) 'Bounded exact-target WHM/BRD/NIN reactive-CC reservation runtime'
if ($normalizedMiracleIntercept -notmatch 'counterActionId = ResolveCounterActionId\( localJobId, miracleMetadataVerified, silentNocturneMetadataVerified\); var protectionMetadataReady = RequiredProtectionStatusIds\(counterActionId\)\.All\( verifiedProtectionStatusIds\.Contains\); var enabled = configurationEnabled && isCrystallineConflict && localIdentityValid && counterActionId != 0 && protectionMetadataReady;' -or
    $normalizedMiracleIntercept -notmatch 'var contradanceEnabled = enableContradance && contradanceMetadataVerified;.*?if \(activeThreat is \{ \} staleThreatBeforeDrain && \(nowMilliseconds < staleThreatBeforeDrain\.ObservedAtMilliseconds \|\| nowMilliseconds - staleThreatBeforeDrain\.ObservedAtMilliseconds >= ThreatLifetime\(staleThreatBeforeDrain\)\)\).*?RecordExpired\(staleThreatBeforeDrain\); activeThreat = null;.*?if \(activeThreat is \{ \} disabledThreatBeforeDrain && !IsThreatKindEnabled\( disabledThreatBeforeDrain\.Kind, marksmanSpiteEnabled, zantetsukenEnabled, furiousBacklashEnabled, contradanceEnabled, cleanseFollowupEnabled, guardFollowupEnabled\)\).*?activeThreat = null;.*?if \(activeThreat is \{ \} frozenThreatBeforeDrain\).*?TryRefreshAndResolveFrozenThreat\( localPlayer!, frozenThreatBeforeDrain, out var refreshedThreatBeforeDrain, out _\).*?activeThreat = refreshedThreatBeforeDrain;.*?retired before drain after exact job/action/actor drift.*?activeThreat = null;.*?var cleanseSignals = DrainThreats\( localPlayer!, marksmanSpiteEnabled, zantetsukenEnabled, furiousBacklashEnabled, contradanceEnabled, cleanseFollowupEnabled, nowMilliseconds, episodeGameplayKeyToken\); DrainConfirmations\(nowMilliseconds\);.*?nowMilliseconds = Math\.Max\(nowMilliseconds, Environment\.TickCount64\); if \(activeThreat is \{ \} expiringThreat && \(nowMilliseconds < expiringThreat\.ObservedAtMilliseconds \|\| nowMilliseconds - expiringThreat\.ObservedAtMilliseconds >= ThreatLifetime\(expiringThreat\)\)\).*?RecordExpired\(expiringThreat\); activeThreat = null;' -or
    $miracleIntercept -match '\bShowCcProtection\b') {
    throw 'Reactive CC must require exact WHM/BRD/NIN metadata and retire expired/disabled/drifted actor-action leases before draining new packets; the post-drain hook-time expiry check must still enforce the same original lifetime.'
}
if ($normalizedMiracleIntercept -notmatch 'var cleanseFollowupEnabled = enabled && enablePostPurifyCrowdControl && purifyMetadataVerified;' -or
    $normalizedMiracleIntercept -notmatch 'capture\.SetMiracleCleanseFollowupLocalEntityId\( cleanseFollowupEnabled && localAlive \? localPlayer!\.EntityId : 0\)') {
    throw 'Post-Purify CC capture must remain separately gated by its toggle, verified Purify metadata, live WHM/BRD/NIN identity, and CC-only master.'
}
if ($normalizedMiracleIntercept -notmatch 'var guardFollowupEnabled = enabled && enablePostGuardCrowdControl && verifiedProtectionStatusIds\.Contains\( MiracleGuardFollowupRules\.GuardStatusId\) && verifiedProtectionStatusIds\.Contains\( MiracleGuardFollowupRules\.GuardStatusAlternateId\);' -or
    $normalizedMiracleIntercept -notmatch 'var protectionEndFollowupEnabled = cleanseFollowupEnabled \|\| guardFollowupEnabled; var protectionEndJobChanged = protectionEndFollowupEnabled && protectionEndLocalJobId != 0 && protectionEndLocalJobId != localJobId; if \(protectionEndJobChanged\).*?inputFrame\.Consume\(\);.*?activeThreat = null;.*?ClearCleanseFollowupStates\(\);.*?guardFollowupState = MiracleGuardFollowupState\.Initial;.*?ClearProtectionEndDiagnostics\(\);.*?protectionEndLocalJobId = protectionEndFollowupEnabled \? localJobId : 0;') {
    throw 'Both protection-end subtypes must be separately metadata-gated and a local WHM/BRD/NIN job change must retire shared input, active work, all Purify/Guard episodes, consent, and rank diagnostics.'
}
if ($normalizedMiracleIntercept -notmatch 'ObserveProtectionEndHeldConsent\( allowHeldGameplayKey && localAlive && \(cleanseFollowupEnabled \|\| guardFollowupEnabled\), inputFrame, hardReset \|\| protectionEndJobChanged\);' -or
    $normalizedMiracleIntercept -notmatch 'var latchedKeyPhysicallyDown = TryGetLatchedProtectionEndKey\(out var previousKey\) && inputFrame\.IsGameplayKeyPhysicallyDown\(previousKey\); var eligibleKey = VirtualKey\.NO_KEY; if \(enabled && !input\.IsTextInputActive\).*?input\.ProbeSucceeded && input\.HeldGameplayKeyEligible \? input\.HeldGameplayKey : input\.ProbeSucceeded && input\.FreshGameplayKeyPressed \? input\.FreshGameplayKey : VirtualKey\.NO_KEY;.*?IsExactVirtualKey\(observedKey\) && inputFrame\.IsGameplayKeyPhysicallyDown\(observedKey\).*?MiracleProtectionEndRules\.ObserveHeldConsent\( protectionEndHeldConsent, new MiracleProtectionEndHeldConsentObservation\( enabled, input\.IsTextInputActive, eligibleKey == VirtualKey\.NO_KEY \? 0 : \(int\)eligibleKey, latchedKeyPhysicallyDown, hardReset\)\)' -or
    $normalizedMiracleIntercept -notmatch 'private int ResolveEpisodeGameplayKeyToken\(.*?!input\.ProbeSucceeded \|\| input\.IsTextInputActive.*?TryGetLatchedProtectionEndKey\(out var latchedKey\) && inputFrame\.IsGameplayKeyGenerationEligible\(latchedKey\).*?input\.HeldGameplayKeyEligible \? input\.HeldGameplayKey : input\.FreshGameplayKeyPressed \? input\.FreshGameplayKey : VirtualKey\.NO_KEY; return IsExactVirtualKey\(candidate\) && inputFrame\.IsGameplayKeyGenerationEligible\(candidate\)' -or
    $normalizedMiracleIntercept -notmatch 'private static bool IsReservedGameplayKeyPhysicallyDown\(.*?IsExactVirtualKey\(key\) && inputFrame\.IsGameplayKeyGenerationEligible\(key\)' -or
    $normalizedMiracleProtectionEndRules -notmatch 'public static bool DispatchConsumesHeldConsent\(MiracleInterceptThreatKind threat\) => false;') {
    throw 'Protection-end pre-arm may observe the immutable raw frame despite a same-frame Purify claim. Purify/Guard remember the actor episode first, then may bind only the current eligible, unconsumed, non-chat-poisoned generation at authoritative protection end or inside the original 500-ms opportunity.'
}
if ($normalizedMiracleIntercept -notmatch 'if \(!enabled\).*?ClearCleanseFollowupStates\(\);.*?guardFollowupState = MiracleGuardFollowupState\.Initial;.*?protectionEndLocalJobId = 0;.*?ClearProtectionEndDiagnostics\(\);' -or
    $normalizedMiracleIntercept -notmatch 'if \(!localAlive\).*?ClearCleanseFollowupStates\(\);.*?guardFollowupState = MiracleGuardFollowupState\.Initial;.*?protectionEndLocalJobId = 0;.*?ClearProtectionEndDiagnostics\(\);' -or
    $normalizedMiracleIntercept -notmatch 'private void ResetRuntime\(\).*?activeThreat = null;.*?ClearCleanseFollowupStates\(\);.*?guardFollowupState = MiracleGuardFollowupState\.Initial;.*?protectionEndLocalJobId = 0;.*?ClearProtectionEndDiagnostics\(\);') {
    throw 'Configuration/context/action-metadata failure, own Guard via the coordinator gate, death, job change, and hard reset must clear protection-end episodes, exact held consent, and frozen rank state.'
}
if ($normalizedMiracleIntercept -notmatch 'var followupPromotions = new List<MiracleFollowupPromotion>\(2\); foreach \(var cleanseSignal in cleanseSignals\).*?ObserveCleanseFollowup\(.*?trackedSlot: null, inputFrame, episodeGameplayKeyToken\).*?foreach \(var cleanseSlot in cleanseFollowupStates\.Keys\.Order\(\)\.ToArray\(\)\).*?ObserveCleanseFollowup\(.*?cleanseSlot, inputFrame, episodeGameplayKeyToken\).*?ObserveGuardFollowup\(.*?inputFrame, episodeGameplayKeyToken\).*?if \(activeThreat is null && followupPromotions\.Count > 0\).*?var selected = SelectFollowupPromotion\(followupPromotions\); activeThreat = selected\.Threat; protectionEndLastRank = selected\.Rank;.*?foreach \(var retired in followupPromotions\).*?if \(retired == selected\) continue;.*?rejectedThreatCount' -or
    $normalizedMiracleIntercept -notmatch 'private static MiracleFollowupPromotion SelectFollowupPromotion\(.*?var selected = promotions\[0\];.*?for \(var index = 1; index < promotions\.Count; index\+\+\).*?CompareFollowupPromotions\(promotions\[index\], selected\) < 0.*?return selected;.*?MiracleProtectionEndRules\.Compare\(left\.Rank, right\.Rank\)') {
    throw 'All independently tracked Purify slots and the Guard release set must enter one common rank pass, arm exactly one winner, and terminally retire every simultaneous loser without fallback.'
}
if ([regex]::Matches($miracleIntercept, 'cleanseFollowupStates\[enemySlot\]\s*=\s*decision\.NextState').Count -ne 1 -or
    $normalizedMiracleIntercept -notmatch 'if \(!EnemySlotRules\.IsValidSlot\(enemySlot\)\) return null;.*?cleanseFollowupStates\.TryGetValue\(enemySlot, out var tracked\).*?cleanseFollowupStates\.Remove\(enemySlot\).*?cleanseFollowupStates\[enemySlot\] = decision\.NextState' -or
    $normalizedMiracleIntercept -notmatch 'private void ClearCleanseFollowupStates\(\).*?cleanseFollowupStates\.Clear\(\);.*?pendingCleanseTargetResolutions\.Clear\(\);.*?cleanseFollowupSignalLedger = MiracleCleanseFollowupSignalLedger\.Initial;') {
    throw 'Post-Purify runtime state must remain one bounded dictionary entry per exact valid S1-S5 slot, at most five pending exact canonical resolutions, and bounded deduplication; full reset clears all three stores.'
}
if ($normalizedMiracleIntercept -notmatch 'ResolvePendingCleanseTargets\( localPlayer, enablePostPurifyCrowdControl, nowMilliseconds, cleanseSignals\);.*?while \(capture\.TryDequeueMiracleInterceptThreat\(out var signal\)\)' -or
    $normalizedMiracleIntercept -notmatch 'var cleanseSignalKey = new MiracleCleanseFollowupSignalKey\( signal\.CasterEntityId, signal\.ActionId, signal\.EventTargetEntityId, signal\.EffectType, signal\.EffectValue, signal\.GlobalSequence, signal\.SourceSequence\); var retirement = MiracleCleanseFollowupRules\.RetireValidatedSignal\( cleanseFollowupSignalLedger, cleanseSignalKey\); cleanseFollowupSignalLedger = retirement\.NextState; if \(!retirement\.IsNewValidatedSignal\) continue; var pendingResolution = new MiracleCleanseFollowupPendingResolution\( cleanseSignalKey, signal\.ObservedAtMilliseconds, signal\.LocalEntityId, localPlayer\.ClassJob\.RowId, signal\.FeatureGeneration\); var resolution = ResolveCleanseTarget\( pendingResolution, localPlayer, enablePostPurifyCrowdControl, eventNow\); if \(resolution\.DidResolve && resolution\.ResolvedSignal is \{ \} resolvedSignal\).*?cleanseSignals\.Add\(resolvedSignal\);.*?if \(resolution\.ShouldRetry && pendingCleanseTargetResolutions\.Count < MiracleCleanseFollowupRules\.MaximumPendingResolutions\).*?pendingCleanseTargetResolutions\.Add\(pendingResolution\);' -or
    $normalizedMiracleIntercept -notmatch 'private void ResolvePendingCleanseTargets\(.*?while \(index < pendingCleanseTargetResolutions\.Count\).*?var decision = ResolveCleanseTarget\( pending, localPlayer, configurationEnabled, Math\.Max\(nowMilliseconds, Environment\.TickCount64\)\); if \(decision\.ShouldRetry\).*?index\+\+; continue;.*?pendingCleanseTargetResolutions\.RemoveAt\(index\); if \(decision\.DidResolve && decision\.ResolvedSignal is \{ \} resolvedSignal\).*?resolvedSignals\.Add\(resolvedSignal\);' -or
    $normalizedMiracleIntercept -notmatch 'private MiracleCleanseFollowupResolutionDecision ResolveCleanseTarget\(.*?var canonical = ResolveUniqueCanonicalCleanseEnemy\(pending\.Key\.CasterEntityId\);.*?return MiracleCleanseFollowupRules\.ResolvePendingSignal\( pending, new MiracleCleanseFollowupResolutionObservation\( configurationEnabled, IsCrystallineConflict: true, IsLocalCounterJobValid: counterActionId != 0 && localJobId != 0, localPlayer\.EntityId, localJobId, capture\.CurrentMiracleCleanseFollowupGeneration, target, nowMilliseconds\)\);' -or
    $normalizedMiracleIntercept -notmatch 'private EnemyHudSnapshot\? ResolveUniqueCanonicalCleanseEnemy\(uint casterEntityId\).*?if \(!executeTracker\.IsActive\) return null;.*?Where\(static enemy => EnemySlotRules\.IsValidSlot\(enemy\.Slot\)\).*?enemy\.EntityId == casterEntityId.*?enemy\.JobId != 0.*?TargetHighlightRules\.IsValidGameObjectId\(enemy\.GameObjectId\).*?Take\(2\).*?if \(matches\.Length != 1\) return null;.*?enemies\.Count\(enemy => enemy\.Slot == match\.Slot\) == 1 && enemies\.Count\(enemy => enemy\.GameObjectId == match\.GameObjectId\) == 1 && enemies\.Count\(enemy => enemy\.EntityId == match\.EntityId\) == 1') {
    throw 'Runtime must retire a validated Purify packet before lookup, retry only that immutable signal against one unique canonical e1-e5 identity inside its original acquisition deadline, remove pending state before lifecycle exposure, and keep duplicates inert under the five-slot cap.'
}
if ($normalizedMiracleIntercept -notmatch 'var immediateThreats = new List<MiracleThreatState>\(\);.*?while \(capture\.TryDequeueMiracleInterceptThreat\(out var signal\).*?var incomingThreat = new MiracleThreatState\(.*?GameplayKeyToken: episodeGameplayKeyToken\); if \(activeThreat is \{ \} previousThreat && previousThreat\.Signal != identity && !MiracleProtectionEndRules\.CanPreemptUnattemptedLowerPriorityThreat\( previousThreat\.Kind, previousThreat\.RetryState, incomingThreat\.Kind\)\).*?rejectedThreatCount.*?continue;.*?immediateThreats\.Add\(incomingThreat\);.*?if \(immediateThreats\.Count > 0\).*?OrderByDescending\(static threat => MiracleInterceptRules\.GetDispatchPriority\(threat\.Kind\)\).*?ThenBy\(static threat => threat\.ObservedAtMilliseconds\).*?ThenBy\(static threat => threat\.EnemySlot\).*?ThenBy\(static threat => threat\.GameObjectId\).*?First\(\); var preemptedThreat = activeThreat; activeThreat = selected;.*?foreach \(var retired in immediateThreats\).*?if \(retired == selected\) continue;.*?rejectedThreatCount' -or
    $normalizedMiracleInterceptRules -notmatch 'MarksmanSpite or MiracleInterceptThreatKind\.Zantetsuken or MiracleInterceptThreatKind\.FuriousBacklash => 3, MiracleInterceptThreatKind\.Contradance => 2, MiracleInterceptThreatKind\.PostPurifyCrowdControl or MiracleInterceptThreatKind\.PostGuardCrowdControl => 1') {
    throw 'All urgent events drained together must first remember exact actor/action/event data, then select one deterministic priority winner via GetDispatchPriority; every simultaneous loser is terminal. A strictly higher-priority urgent event may replace only an unattempted lower-priority reactive lease.'
}
if ($normalizedMiracleIntercept -notmatch 'if \(activeThreat is \{ GameplayKeyToken: <= 0 \} unboundThreat\).*?TryRefreshAndResolveFrozenThreat\( localPlayer, unboundThreat, out var refreshedThreat, out _\).*?activeThreat = refreshedThreat;.*?keyless lease retired after exact identity drift.*?activeThreat = null;.*?while \(capture\.TryDequeueMiracleInterceptThreat' -or
    $normalizedMiracleIntercept -notmatch 'if \(activeThreat is not \{ \} threat\).*?if \(threat\.GameplayKeyToken <= 0\).*?TryRefreshAndResolveFrozenThreat\( localPlayer!, threat, out var refreshedKeylessThreat, out _\).*?activeThreat = null;.*?threat = refreshedKeylessThreat; activeThreat = threat;.*?if \(episodeGameplayKeyToken <= 0\).*?Waiting: exact threat stored; no eligible held/fresh key yet.*?threat = threat with \{ GameplayKeyToken = episodeGameplayKeyToken \}; activeThreat = threat;.*?var input = inputFrame\.Snapshot; var triggerKey = threat\.GameplayKeyToken > 0 \? \(VirtualKey\)threat\.GameplayKeyToken : VirtualKey\.NO_KEY; if \(!IsExactVirtualKey\(triggerKey\) \|\| !inputFrame\.IsGameplayKeyGenerationEligible\(triggerKey\)\).*?activeThreat = null;.*?if \(!dispatchAllowed\)' -or
    $normalizedMiracleIntercept -notmatch 'private bool TryRefreshAndResolveFrozenThreat\(.*?var currentLocalJobId = localPlayer\.ClassJob\.IsValid \? localPlayer\.ClassJob\.RowId : 0;.*?refreshedThreat\.CounterActionId != counterActionId \|\| refreshedThreat\.LocalJobId != currentLocalJobId.*?return false;.*?candidate = ResolveCandidate\(localPlayer, refreshedThreat\); return candidate is not null;') {
    throw 'An urgent reactive-CC episode must revalidate exact current job/action/actor while keyless, may bind the first eligible key only inside its original lease, and after binding must terminally validate that exact generation before yielding to another helper.'
}
$enemyHudSnapshot = Read-RequiredSource (Join-Path $pluginServicesRoot 'EnemyHudSnapshot.cs') 'Enemy HUD rank snapshot'
$executeTracker = Read-RequiredSource (Join-Path $pluginServicesRoot 'ExecuteTracker.cs') 'Enemy HUD tracker'
$normalizedExecuteTracker = $executeTracker -replace '\s+', ' '
Assert-Literals $enemyHudSnapshot @('internal bool HasTrustedMp { get; init; }') 'Identity-bound enemy MP trust flag'
if ($normalizedExecuteTracker -notmatch 'var exactMpIdentityPreviouslyTrusted = state\.TrustedMpGameObjectId == player\.GameObjectId && state\.TrustedMpEntityId == player\.EntityId && state\.TrustedMpJobId == playerJobId; var rankingTrustedMp = player\.MaxMp == CombatFrameRules\.ExpectedMaximumMp && player\.CurrentMp <= player\.MaxMp && \(player\.CurrentMp > 0 \|\| exactMpIdentityPreviouslyTrusted\); if \(rankingTrustedMp\).*?state\.TrustedMpGameObjectId = player\.GameObjectId;.*?state\.TrustedMpEntityId = player\.EntityId;.*?state\.TrustedMpJobId = playerJobId;' -or
    $normalizedExecuteTracker -notmatch 'HasTrustedMp = rankingTrustedMp') {
    throw 'Protection-end MP ranking may trust only exact-10,000 PvP MP bound to unchanged GOID/entity/job identity; zero needs prior trust and sentinel MP must stay unknown.'
}
if ($normalizedMiracleIntercept -notmatch 'private IPlayerCharacter\? ResolveCleanseFollowupCandidate\( IPlayerCharacter localPlayer, MiracleCleanseFollowupTargetIdentity target\).*?enemy\.GameObjectId == target\.GameObjectId && enemy\.EntityId == target\.EntityId && enemy\.JobId == target\.JobId.*?Take\(2\).*?if \(canonical\.Length != 1\) return null;.*?player\.GameObjectId == target\.GameObjectId && player\.EntityId == target\.EntityId && player\.GameObjectId != localPlayer\.GameObjectId && player\.ClassJob\.IsValid && player\.ClassJob\.RowId == target\.JobId.*?Take\(2\).*?return players\.Length == 1 && IsLivePlayer\(players\[0\]\) && HasValidNativeIdentity\(players\[0\]\)') {
    throw 'Post-Purify status observation must resolve exactly one unchanged canonical e1-e5 and exactly one matching live native player actor.'
}
if ($normalizedMiracleIntercept -notmatch 'var blockerFamily = BlockerFamilyForAction\(threat\.CounterActionId\); var anyProtection = HasAnyVerifiedCcProtection\(candidate, blockerFamily\); var guardReappeared = threat\.Kind == MiracleInterceptThreatKind\.PostGuardCrowdControl && CountActiveGuardStatuses\(candidate\) != 0;.*?var otherProtection = \(anyProtection && !hardenedScales\) \|\| guardReappeared;.*?var rangeAndLineOfSight = HasActionRangeAndLineOfSight\( threat\.CounterActionId, localPlayer!, candidate\); var structurallyReady = HasStructuralActionReadiness\(localPlayer!, threat\.CounterActionId\); var exactIntentCanProgress = !hardenedScales && !otherProtection && rangeAndLineOfSight && structurallyReady; var globallyQueueReady = exactIntentCanProgress && HasGlobalQueueReadiness\( localPlayer!, threat\.CounterActionId\);' -or
    $normalizedMiracleIntercept -notmatch 'var revalidatedProtection = revalidated is not null && HasAnyVerifiedCcProtection\(revalidated, blockerFamily\); var revalidatedGuardAbsent = revalidated is not null && \(threat\.Kind != MiracleInterceptThreatKind\.PostGuardCrowdControl \|\| CountActiveGuardStatuses\(revalidated\) == 0\);.*?var revalidatedRange = revalidated is not null && HasActionRangeAndLineOfSight\( threat\.CounterActionId, localPlayer!, revalidated\);.*?var revalidatedActionIdentity = threat\.CounterActionId == counterActionId && threat\.LocalJobId == revalidatedLocalJobId; var revalidatedInput = !input\.IsTextInputActive && IsExactVirtualKey\(triggerKey\) && threat\.GameplayKeyToken == \(int\)triggerKey && inputFrame\.IsGameplayKeyGenerationEligible\(triggerKey\); var revalidatedInsideWindow = revalidationNow >= threat\.ObservedAtMilliseconds && revalidationNow - threat\.ObservedAtMilliseconds < ThreatLifetime\(threat\); var finalValidationPassed = revalidated is not null && !revalidatedHardened && !revalidatedProtection && revalidatedGuardAbsent && revalidatedRange && revalidatedActionIdentity && revalidatedInput && revalidatedInsideWindow;') {
    throw 'Reactive CC must freeze and finally revalidate counter action/local job, exact actor/life, action-specific blockers, post-Guard absence, range/LoS, exact eligible key generation, and the strict original window before every bounded native call.'
}
if ($normalizedMiracleIntercept -notmatch 'return pressureTracker\.TryGetFreshTeamTargetCount\( new TargetPressureActorIdentity\(localPlayer\.GameObjectId, localPlayer\.EntityId\), new TargetPressureActorIdentity\(candidate\.GameObjectId, candidate\.EntityId\), nowMilliseconds, MaximumTeamPressureAgeMilliseconds, out teamTargetCount\);' -or
    $normalizedMiracleIntercept -notmatch 'CounterActionReachable = IsCounterActionReachable\( localPlayer, player\).*?teamTargetCountKnown = TryGetFreshTeamTargetCount\( localPlayer, player, nowMilliseconds, out cleanseFollowupTeamPressure\);.*?new MiracleProtectionEndRankCandidate\( MiracleInterceptThreatKind\.PostPurifyCrowdControl, canonical\.Slot, promotion\.Target\.GameObjectId, promotion\.Target\.EntityId, promotion\.Target\.JobId, teamTargetCountKnown, cleanseFollowupTeamPressure, player\.CurrentHp, player\.MaxHp, canonical\.HasTrustedMp, canonical\.CurrentMp, canonical\.MaxMp\)' -or
    $normalizedMiracleIntercept -notmatch 'var teamTargetCountKnown = live && TryGetFreshTeamTargetCount\( localPlayer, player!, nowMilliseconds, out teamTargetCount\);.*?TeamTargetCountKnown.*?HasTrustedMp = live && enemy\.HasTrustedMp') {
    throw 'Both protection-end paths must preserve at-most-250-ms exact pressure as known-or-unknown rank data, plus current HP and identity-bound trusted PvP MP; pressure is never a hard gate.'
}
if ($miracleIntercept -match '\b(HasFreshExactTeamFocus|RequiresFreshTeamFocus|RequiredTeamTargetCount)\b' -or
    $normalizedMiracleIntercept -match 'teamTargetCount\s*>=\s*[12]\b') {
    throw 'Reactive protection-end dispatch must not restore a team-focus minimum; unknown remains eligible below every known sample and known zero is valid.'
}
if ($miracleIntercept -match '\bGetTargetId\s*\(' -or
    $miracleIntercept -match 'pressureTracker\.GetTeamTargetCount\s*\(') {
    throw 'Post-Purify and post-Guard reactive CC must not require or read the local selected/hard target, nor use the stale raw team-count accessor.'
}
if ($normalizedMiracleIntercept -notmatch 'private IReadOnlyList<MiracleGuardFollowupCandidate> BuildGuardFollowupCandidates\(.*?executeTracker\.IsActive.*?EnemySlotRules\.IsValidSlot\(enemy\.Slot\).*?ambiguousSlots.*?ambiguousGameObjectIds.*?ambiguousEntityIds.*?ResolveGuardFollowupCandidate\(localPlayer, target\).*?TryGetFreshTeamTargetCount\( localPlayer, player!, nowMilliseconds, out teamTargetCount\).*?CountActiveGuardStatuses\(player!, out guardRemainingMilliseconds\).*?GuardRemainingMilliseconds = live \? guardRemainingMilliseconds : 0.*?ReservationGameplayKeyToken = episodeGameplayKeyToken.*?IsReservedGameplayKeyPhysicallyDown\( ownedGameplayKeyToken, inputFrame\).*?CounterActionReachable = live && IsCounterActionReachable\( localPlayer, player!\)' -or
    $normalizedMiracleIntercept -notmatch 'private bool IsCounterActionReachable\( IPlayerCharacter localPlayer, IPlayerCharacter candidate\).*?counterActionId == EnemyCombatConstants\.NinjaAeolianEdgeComboCarrierActionId \? EnemyCombatConstants\.ForkedRaijuActionId : counterActionId; return MiracleInterceptConfirmationRules\.ExpectedStatusForAction\(rangeActionId\) != 0 && !HasAnyVerifiedCcProtection\( candidate, BlockerFamilyForAction\(rangeActionId\)\) && HasActionRangeAndLineOfSight\(rangeActionId, localPlayer, candidate\);' -or
    $normalizedMiracleIntercept -notmatch 'private static int CountActiveGuardStatuses\( IPlayerCharacter player, out long longestRemainingMilliseconds\).*?foreach \(var status in player\.StatusList\).*?MiracleGuardFollowupRules\.IsExactGuardStatus\(status\.StatusId\).*?count\+\+;.*?ValidatedProtectionRemainingMilliseconds\( status\.StatusId, status\.RemainingTime\).*?if \(count > 1\) return count') {
    throw 'Post-Guard observation must use exact unambiguous S1-S5 identity, exact Guard 3054/3673 live membership, release-time exact key ownership, blocker-free native range/LoS reachability before ranking, and only a bounded advisory duration.'
}
if ($normalizedMiracleIntercept -notmatch 'guardFollowupState = decision\.NextState;.*?if \(!decision\.ShouldPromote \|\| decision\.PromotionIntent is not \{ \} promotion\) return null;.*?ResolveCanonicalEnemy\(promotion\.Target\).*?ResolveGuardFollowupCandidate\(localPlayer, promotion\.Target\).*?CountActiveGuardStatuses\(player\) != 0.*?new MiracleProtectionEndRankCandidate\( MiracleInterceptThreatKind\.PostGuardCrowdControl, promotion\.Target\.EnemySlot, promotion\.Target\.GameObjectId, promotion\.Target\.EntityId, promotion\.Target\.JobId, promotion\.TeamTargetCountKnown, promotion\.TeamTargetCount, promotion\.CurrentHp, promotion\.MaximumHp, promotion\.HasTrustedMp, promotion\.CurrentMp, promotion\.MaximumMp\).*?new MiracleThreatState\( MiracleInterceptThreatKind\.PostGuardCrowdControl, promotion\.Target\.GameObjectId, promotion\.Target\.EntityId, promotion\.Target\.JobId, promotion\.Target\.EnemySlot, promotion\.ReleasedAtMilliseconds') {
    throw 'Post-Guard promotion must consume Core state first, revalidate the same frozen S-slot/GOID/entity/job actor and exact Guard absence, then carry one immutable common-rank record without reranking or fallback.'
}
if ($normalizedMiracleIntercept -notmatch 'var incomingThreat = new MiracleThreatState\( kind, canonical\.GameObjectId, canonical\.EntityId, canonical\.JobId, canonical\.Slot, signal\.ObservedAtMilliseconds.*?GameplayKeyToken: episodeGameplayKeyToken\);.*?immediateThreats\.Add\(incomingThreat\);' -or
    $normalizedMiracleIntercept -notmatch 'new MiracleThreatState\( MiracleInterceptThreatKind\.PostPurifyCrowdControl, promotion\.Target\.GameObjectId, promotion\.Target\.EntityId, promotion\.Target\.JobId, canonical\.Slot, promotion\.ReleasedAtMilliseconds' -or
    $normalizedMiracleIntercept -notmatch 'private IPlayerCharacter\? ResolveCandidate\(.*?EnemySlotRules\.IsValidSlot\(threat\.EnemySlot\) && enemy\.Slot == threat\.EnemySlot && enemy\.GameObjectId == threat\.GameObjectId && enemy\.EntityId == threat\.EntityId && enemy\.JobId == threat\.JobId.*?player\.GameObjectId == threat\.GameObjectId && player\.EntityId == threat\.EntityId.*?player\.ClassJob\.RowId == threat\.JobId.*?IsLivePlayer\(players\[0\]\) && HasValidNativeIdentity\(players\[0\]\)') {
    throw 'Every reactive-CC arming path must freeze the exact canonical S1-S5 slot with GOID/entity/job, and final resolution must require that same living native identity.'
}
if ($normalizedMiracleIntercept -notmatch 'if \(localJobId == EnemyCombatConstants\.NinjaJobId\).*?!verifiedCounterActionIds\.Contains\( EnemyCombatConstants\.ForkedRaijuActionId\) \|\| !verifiedCounterActionIds\.Contains\( EnemyCombatConstants\.FleetingRaijuActionId\).*?return 0;.*?GetAdjustedActionId\( EnemyCombatConstants\.NinjaAeolianEdgeComboCarrierActionId\).*?IsExactRaijuAction\(adjustedActionId\) && verifiedCounterActionIds\.Contains\(adjustedActionId\) \? adjustedActionId : EnemyCombatConstants\.NinjaAeolianEdgeComboCarrierActionId;.*?EnemyCombatConstants\.WhiteMageJobId when miracleMetadataVerified => EnemyCombatConstants\.MiracleOfNatureActionId, EnemyCombatConstants\.BardJobId when silentNocturneMetadataVerified => EnemyCombatConstants\.SilentNocturneActionId' -or
    $normalizedMiracleIntercept -notmatch 'private static unsafe bool HasStructuralActionReadiness\( IPlayerCharacter localPlayer, uint actionId\).*?if \(actionId == EnemyCombatConstants\.ForkedRaijuActionId && localPlayer\.StatusList\.Any\(static status => status\.StatusId == EnemyCombatConstants\.SealedForkedRaijuStatusId\)\).*?return false;.*?if \(IsExactRaijuAction\(actionId\) && localPlayer\.StatusList\.Any\(static status => status\.StatusId == EnemyCombatConstants\.PvPBindStatusId\)\).*?return false;.*?IsExactRaijuAction\(actionId\) \? actionManager->GetAdjustedActionId\( EnemyCombatConstants\.NinjaAeolianEdgeComboCarrierActionId\) : actionManager->GetAdjustedActionId\(actionId\); return actionManager != null && adjustedActionId == actionId' -or
    $normalizedMiracleIntercept -notmatch 'if \(currentLocalJobId == EnemyCombatConstants\.NinjaJobId && threat\.CounterActionId == EnemyCombatConstants\.NinjaAeolianEdgeComboCarrierActionId && IsExactRaijuAction\(counterActionId\)\).*?threat = threat with \{ CounterActionId = counterActionId \}; activeThreat = threat;.*?if \(threat\.CounterActionId == EnemyCombatConstants\.NinjaAeolianEdgeComboCarrierActionId\).*?no exact Raiju variant exposed by the combo carrier' -or
    $normalizedMiracleIntercept -notmatch 'BlockerFamilyForAction\(uint actionId\) => actionId == EnemyCombatConstants\.MiracleOfNatureActionId \? CcImmunityBrakeBlockerFamily\.Miracle : CcImmunityBrakeBlockerFamily\.StandardPurifyCc;' -or
    $normalizedMiracleIntercept -notmatch 'if \(MiracleInterceptConfirmationRules\.ExpectedStatusForAction\(actionId\) == 0 \|\| !TargetHighlightRules\.IsValidGameObjectId\(targetGameObjectId\)\).*?nearAssist\.RunWithoutRedirect\(\(\) => actionManager->UseAction\( ActionType\.Action, actionId, targetGameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)\)') {
    throw 'Reactive CC may resolve only WHM 29228, BRD 29395, or one Raiju variant exposed by exact NIN combo carrier 29500; both NIN metadata rows, StandardPurify blockers, and exact variant identity are mandatory before one direct request.'
}
$miracleFrameClaim = [regex]::Match($miracleIntercept, 'inputClaimedThisFrame\s*=\s*true\s*;\s*\r?\n\s*inputFrame\.Consume\s*\(\s*\)\s*;')
$miracleTryUse = [regex]::Match($miracleIntercept, '\bTryUseCounterCcOnce\s*\(\s*localPlayer!\s*,\s*threat\.CounterActionId\s*,\s*revalidated!\.GameObjectId')
$miracleNativeCall = [regex]::Match($miracleIntercept, 'actionManager->UseAction\s*\(')
if (-not $miracleFrameClaim.Success -or -not $miracleTryUse.Success -or -not $miracleNativeCall.Success -or
    $miracleFrameClaim.Index -gt $miracleTryUse.Index -or
    $miracleTryUse.Index -gt $miracleNativeCall.Index) {
    throw 'Reactive CC must claim only the current scheduler frame before each bounded revalidated direct-GOID native action attempt.'
}
if ([regex]::Matches($miracleIntercept, '\bnearAssist\.RunWithoutRedirect\s*\(').Count -ne 1 -or
    $miracleIntercept -match '\b(IKeyState|GameInputContextProbe|EmergencyActionInputCoordinator|PhysicalGameplayKeyRules|GetAsyncKeyState)\b') {
    throw 'Reactive CC must reuse the shared EmergencyActionInputFrame and Near Assist redirect-bypass boundary, never create a duplicate raw-key scanner/coordinator.'
}
if ([regex]::Matches($miracleIntercept, 'ClientActionAttemptBoundary\.Capture\s*\(').Count -ne 2 -or
    [regex]::Matches($miracleIntercept, 'ClientActionAttemptBoundaryRules\.Classify\s*\(').Count -ne 1 -or
    [regex]::Matches($miracleIntercept, 'MiracleProtectionEndRules\.CompleteNativeAttempt\s*\(').Count -ne 1 -or
    $normalizedMiracleIntercept -notmatch 'if \(!globallyQueueReady\).*?inputClaimedThisFrame = true; inputFrame\.Consume\(\);.*?if \(!MiracleProtectionEndRules\.CanAttempt\(.*?inputClaimedThisFrame = true; inputFrame\.Consume\(\);' -or
    $normalizedMiracleIntercept -notmatch 'attemptOutcome == MiracleProtectionEndAttemptOutcome\.RetryScheduled' -or
    $miracleIntercept -match '(?-i:\b(RetryAction|RetryDispatch|QueuedAction|QueueAction)\b)' -or
    $miracleIntercept -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=(?!=|>)') {
    throw 'Reactive CC must use the shared complete native fingerprint and clean-false retry policy, claim global waits/throttle without spending retry budget, and never implement a custom queue or mutate a visible target.'
}
if ($miracleIntercept -match '\bstatus\.[A-Za-z_]*Address\b|\b(StatusAddress|StatusInstanceToken)\b|\b(?:Task|Timer|Thread)\b' -or
    $normalizedMiracleIntercept -notmatch 'foreach \(var status in player\.StatusList\).*?verifiedProtectionStatusIds\.Contains\(status\.StatusId\) && CcImmunityBrakeActionCatalog\.IsBlockerStatus\( blockerFamily, status\.StatusId, targetJobId\).*?return true' -or
    $normalizedMiracleIntercept -notmatch 'foreach \(var status in player\.StatusList\).*?status\.StatusId == statusId.*?return true' -or
    $normalizedMiracleIntercept -notmatch 'private static int CountActiveStatuses\( IPlayerCharacter player, uint statusId, out long longestRemainingMilliseconds\).*?foreach \(var status in player\.StatusList\).*?if \(status\.StatusId != statusId\) continue; count\+\+;.*?ValidatedProtectionRemainingMilliseconds\( status\.StatusId, status\.RemainingTime\).*?if \(count > 1\) return count;' -or
    $normalizedMiracleIntercept -notmatch 'private static long ValidatedProtectionRemainingMilliseconds\( uint statusId, float remainingSeconds\).*?!CcProtectionStatusCatalog\.TryGet\(statusId, out var definition\) \|\| !float\.IsFinite\(remainingSeconds\) \|\| remainingSeconds <= 0f \|\| remainingSeconds > definition\.MaximumRemainingTime.*?return 0;.*?Math\.Max\(1L, \(long\)Math\.Ceiling\(\(double\)remainingSeconds \* 1_000d\)\)' -or
    [regex]::Matches($miracleIntercept, '\bstatus\.RemainingTime\b').Count -ne 2) {
    throw 'Reactive-CC blockers and release authority must use live StatusList membership. The only two RemainingTime reads feed one finite, positive, catalog-bounded, non-authorizing advisory helper; no status address or owned timer is allowed.'
}

# The news flash confirms only the exact action-specific counter-CC status add,
# never that the hostile startup or damage was definitely cancelled.
$miracleConfirmationRules = Read-RequiredSource (Join-Path $coreRoot 'MiracleInterceptConfirmationRules.cs') 'Miracle intercept landing confirmation rules'
$normalizedMiracleConfirmationRules = $miracleConfirmationRules -replace '\s+', ' '
Assert-Literals $miracleConfirmationRules @(
    'MiracleOfNatureActionId = 29_228',
    'MiracleOfNatureStatusId = 3_085',
    'SilentNocturneActionId = 29_395',
    'SilenceStatusId = 1_347',
    'ForkedRaijuActionId = 29_510',
    'FleetingRaijuActionId = 29_707',
    'StunStatusId = 1_343',
    'AddStatusEffectType = 0x0E',
    'CorrelationMilliseconds = 1_500',
    'PopupDurationMilliseconds = 1_500',
    'MaximumConfirmedKeys = 128',
    'MiracleInterceptThreatKind.MarksmanSpite',
    'MiracleInterceptThreatKind.Zantetsuken',
    'MiracleInterceptThreatKind.FuriousBacklash',
    'MiracleInterceptThreatKind.Contradance',
    'MiracleInterceptThreatKind.PostPurifyCrowdControl',
    'MiracleInterceptThreatKind.PostGuardCrowdControl',
    'observation.CasterEntityId == pending.LocalCasterEntityId',
    'observation.ActionId == pending.ActionId',
    'observation.TargetEntityId == pending.TargetEntityId',
    'observation.EffectType == AddStatusEffectType',
    'observation.EffectValue == ExpectedStatusForAction(pending.ActionId)',
    'UseActionAccepted &&',
    'ExpectedSourceSequence != 0',
    'observation.SourceSequence == pending.ExpectedSourceSequence',
    'previous.ConfirmedKeys.Contains(key)',
    'AppendBounded(previous.ConfirmedKeys, key)',
    'TotalConfirmed = SaturatingIncrement(previous.TotalConfirmed)',
    'PendingInsideWindow(previous.Pending, nowMilliseconds) is { } activePending',
    'Pending = activePending',
    'This proves only that the counter-CC landed; it never claims the hostile',
    'action was interrupted.'
) 'Exact bounded action-specific reactive-CC landing correlation and popup truth claim'
if ($normalizedMiracleConfirmationRules -notmatch 'observation\.ObservedAtMilliseconds < pending\.AttemptedAtMilliseconds \|\| observation\.ObservedAtMilliseconds - pending\.AttemptedAtMilliseconds > CorrelationMilliseconds' -or
    $normalizedMiracleConfirmationRules -notmatch 'var skip = Math\.Max\(0, previous\.Length - MaximumConfirmedKeys \+ 1\); return previous\.Skip\(skip\)\.Append\(key\)\.ToImmutableArray\(\)' -or
    $normalizedMiracleConfirmationRules -notmatch 'if \(PendingInsideWindow\(previous\.Pending, nowMilliseconds\) is \{ \} activePending\) \{ return None\(previous with \{ Pending = activePending, Popup = ActivePopup\(previous\.Popup, nowMilliseconds\), LastObservedAtMilliseconds = nowMilliseconds, \}\); \} if \(!attempt\.IsValid \|\| attempt\.AttemptedAtMilliseconds != nowMilliseconds\).*?Pending = attempt' -or
    $normalizedMiracleConfirmationRules -notmatch 'UseActionAccepted && ExpectedSourceSequence != 0 && AttemptedAtMilliseconds >= 0' -or
    $normalizedMiracleConfirmationRules -notmatch 'observation\.EffectValue == ExpectedStatusForAction\(pending\.ActionId\) && observation\.SourceSequence == pending\.ExpectedSourceSequence') {
    throw 'Reactive-CC landing correlation must require a client-accepted attempt with an advanced non-zero exact source sequence, be forward-only within 1500 ms, preserve the first active pending attempt, and deduplicate through a bounded 128-key history.'
}
if ($miracleConfirmationRules -match '\b(UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event)\b') {
    throw 'Reactive-CC landing confirmation rules must remain observational and never initiate actions, input, or target changes.'
}
if ($normalizedMiracleConfirmationRules -notmatch 'ExpectedStatusForAction\(uint actionId\) => actionId switch \{ MiracleOfNatureActionId => MiracleOfNatureStatusId, SilentNocturneActionId => SilenceStatusId, ForkedRaijuActionId or FleetingRaijuActionId => StunStatusId, _ => 0, \}' -or
    $normalizedMiracleConfirmationRules -notmatch 'observation\.ActionId == pending\.ActionId.*?observation\.EffectType == AddStatusEffectType.*?observation\.EffectValue == ExpectedStatusForAction\(pending\.ActionId\)') {
    throw 'AddStatus 0x0E confirmation must correlate WHM 29228 to 3085, BRD 29395 to 1347, and each exact NIN Raiju 29510/29707 to Stun 1343.'
}
$miracleConfirmationSelfTests = Read-RequiredSource (
    Join-Path $coreSelfTestRoot 'MiracleInterceptConfirmationSelfTests.cs') 'Reactive-CC confirmation self-tests'
Assert-Literals $miracleConfirmationSelfTests @(
    'public static void NinjaRaijuVariantsRequireExactStunStatus()',
    'MiracleInterceptConfirmationRules.ForkedRaijuActionId',
    'MiracleInterceptConfirmationRules.FleetingRaijuActionId',
    'cannot confirm from Silence',
    'accepted call without exact source sequence cannot claim automation',
    'cannot confirm from a manual source sequence',
    'manual same-action same-target packet cannot confirm helper ownership',
    'pending retains exact helper source sequence',
    'later exact helper source sequence still confirms',
    'confirms only exact Stun',
    'popup keeps exact Raiju variant'
) 'Both exact Raiju/Stun confirmation variants'
Assert-Literals $miracleGuardProgram @(
    'MiracleInterceptConfirmationSelfTests.NinjaRaijuVariantsRequireExactStunStatus'
) 'Raiju confirmation test registration'
$reactiveCcCombatConstants = Read-RequiredSource (
    Join-Path $pluginServicesRoot 'EnemyCombatConstants.cs') 'Reactive-CC action/icon constants'
$reactiveCcOverlayRenderer = Read-RequiredSource $overlayRendererPath 'Reactive-CC landing popup renderer'
Assert-Literals $reactiveCcCombatConstants @(
    'NinjaAeolianEdgeComboCarrierActionId = 29500',
    'ForkedRaijuActionId = 29510',
    'ForkedRaijuActionIconId = 9656',
    'SealedForkedRaijuStatusId = 3195',
    'FleetingRaijuActionId = 29707',
    'FleetingRaijuActionIconId = 9693',
    'PvPBindStatusId = 1345',
    'RaijuRange = 20f'
) 'Exact NIN combo-carrier, Raiju action, icon, and native-range constants'
$normalizedReactiveCcOverlayRenderer = $reactiveCcOverlayRenderer -replace '\s+', ' '
if ($normalizedReactiveCcOverlayRenderer -notmatch 'EnemyCombatConstants\.ForkedRaijuActionId or EnemyCombatConstants\.FleetingRaijuActionId => "STUN"' -or
    $normalizedReactiveCcOverlayRenderer -notmatch 'EnemyCombatConstants\.ForkedRaijuActionId => EnemyCombatConstants\.ForkedRaijuActionIconId, EnemyCombatConstants\.FleetingRaijuActionId => EnemyCombatConstants\.FleetingRaijuActionIconId') {
    throw 'Reactive NIN landing presentation must label both exact Raiju variants as STUN and render each variant with its matching game icon.'
}
if ($normalizedMiracleConfirmationRules -notmatch 'MiracleInterceptThreatKind\.PostPurifyCrowdControl or MiracleInterceptThreatKind\.PostGuardCrowdControl\) && \(Threat != MiracleInterceptThreatKind\.PostPurifyCrowdControl \|\| RemovedStatusId == 0 \|\| MiracleCleanseFollowupRules\.IsPurifyRemovableStatus\(RemovedStatusId\)\)') {
    throw 'Shared landing confirmation must accept post-Guard without a synthetic removed status and post-Purify with either the exact action-level sentinel or one reviewed removed status; live Resilience remains mandatory upstream.'
}
Assert-Literals $miracleIntercept @(
    'MiracleInterceptConfirmationState.Initial',
    'MiracleInterceptConfirmationRules.ObserveTime(',
    'capture.TryDequeueMiracleInterceptConfirmation(out var effect)',
    'MiracleInterceptConfirmationRules.CorrelationMilliseconds',
    'MiracleInterceptConfirmationRules.ObserveActionEffect(',
    'new MiracleInterceptLandedObservation(',
    'MiracleInterceptConfirmationRules.RegisterAttempt(',
    'new MiracleInterceptPendingAttempt(',
    'ConfirmationPopup = confirmationState.Popup',
    'ConfirmedLandingCount = confirmationState.TotalConfirmed',
    'ConfirmationQueueDepth = capture.MiracleInterceptConfirmationQueueDepth',
    'CapturedConfirmationCount = capture.CapturedMiracleInterceptConfirmations',
    'DroppedConfirmationCount = capture.DroppedMiracleInterceptConfirmations',
    'bool dispatchAllowed',
    'confirmationPendingForLocalCaster',
    'enabled && (localAlive || confirmationPendingForLocalCaster)',
    'Waiting for exact reactive-CC landing evidence'
) 'Reactive-CC landing runtime correlation and diagnostics'
$miracleRegisterIndex = $normalizedMiracleIntercept.IndexOf('MiracleInterceptConfirmationRules.RegisterAttempt(')
$miracleTryUseIndex = $normalizedMiracleIntercept.IndexOf('TryUseCounterCcOnce( localPlayer!, threat.CounterActionId, revalidated!.GameObjectId')
if ($miracleTryUseIndex -lt 0 -or $miracleRegisterIndex -le $miracleTryUseIndex -or
    $normalizedMiracleIntercept -notmatch 'if \(attempted && accepted && revalidated is not null && attemptedAtMilliseconds >= 0\).*?new MiracleInterceptPendingAttempt\( localPlayer!\.EntityId, threat\.CounterActionId, revalidated\.GameObjectId, revalidated\.EntityId, threat\.Kind, accepted, attemptedAtMilliseconds, expectedSourceSequence\)' -or
    $normalizedMiracleIntercept -notmatch 'var boundaryBefore = ClientActionAttemptBoundary\.Capture\(actionManager, actionId\);.*?var accepted = nearAssist\.RunWithoutRedirect.*?var boundaryAfter = ClientActionAttemptBoundary\.Capture\(actionManager, actionId\); if \(accepted && boundaryAfter\.LastUsedActionSequence != 0 && boundaryAfter\.LastUsedActionSequence != boundaryBefore\.LastUsedActionSequence\).*?expectedSourceSequence = boundaryAfter\.LastUsedActionSequence') {
    throw 'Reactive-CC confirmation may register only after a client-accepted native attempt against the revalidated exact target and must carry only the non-zero LastUsedActionSequence that advanced across that exact request.'
}
$miracleDrainConfirmationIndex = $normalizedMiracleIntercept.IndexOf('DrainConfirmations(nowMilliseconds)')
$miracleFollowupIndex = $normalizedMiracleIntercept.IndexOf('ObserveCleanseFollowup(')
$miracleNoThreatIndex = $normalizedMiracleIntercept.IndexOf('if (activeThreat is not { } threat)')
$miracleDispatchGateIndex = $normalizedMiracleIntercept.IndexOf('if (!dispatchAllowed)')
if ($miracleDrainConfirmationIndex -lt 0 -or
    $miracleFollowupIndex -le $miracleDrainConfirmationIndex -or
    $miracleNoThreatIndex -le $miracleFollowupIndex -or
    $miracleDispatchGateIndex -le $miracleDrainConfirmationIndex -or
    $normalizedMiracleIntercept -notmatch 'if \(activeThreat is \{ \} expiringThreat && \(nowMilliseconds < expiringThreat\.ObservedAtMilliseconds \|\| nowMilliseconds - expiringThreat\.ObservedAtMilliseconds >= ThreatLifetime\(expiringThreat\)\)\).*?RecordExpired\(expiringThreat\); activeThreat = null;.*?var followupPromotions = new List<MiracleFollowupPromotion>\(2\);.*?ObserveCleanseFollowup\( localPlayer!, cleanseFollowupEnabled, activeThreat is not null,.*?ObserveGuardFollowup\( localPlayer!, guardFollowupEnabled, activeThreat is not null, nowMilliseconds, inputFrame, episodeGameplayKeyToken\);.*?if \(activeThreat is not \{ \} threat\) return Publish\("Waiting", "No current exact threat", nowMilliseconds\);.*?if \(!dispatchAllowed\).*?RecordWait\(threat, MiracleWaitReason\.HigherPriorityHelper\);.*?return Publish\("Armed", "Waiting: higher-priority helper claimed this frame", nowMilliseconds\);' -or
    $normalizedMiracleIntercept -notmatch 'if \(!localAlive\).*?if \(confirmationPendingForLocalCaster\) DrainConfirmations\(nowMilliseconds\);.*?"Waiting for exact reactive-CC landing evidence"') {
    throw 'Every Purify/Guard follow-up frame must run before dispatch; urgent/helper priority may only retain the frozen intent inside its bounded lease, while local death must preserve exact pending landing evidence.'
}
if ([regex]::Matches($normalizedMiracleIntercept, 'ObserveCleanseFollowup\( localPlayer!, cleanseFollowupEnabled, activeThreat is not null,').Count -ne 2) {
    throw 'Both new-signal and ordinary-frame follow-up observations must remain independent of scheduler-frame ownership while yielding to an already frozen reactive threat.'
}
if ([regex]::Matches($normalizedMiracleIntercept, 'ObserveGuardFollowup\( localPlayer!, guardFollowupEnabled, activeThreat is not null, nowMilliseconds, inputFrame, episodeGameplayKeyToken\)').Count -ne 1) {
    throw 'The live framework may observe post-Guard release exactly once per frame while yielding only to an already frozen reactive threat.'
}
if ($normalizedMiracleIntercept -notmatch 'if \(decision\.NextState\.ActiveSignal is null\) cleanseFollowupStates\.Remove\(enemySlot\); else cleanseFollowupStates\[enemySlot\] = decision\.NextState;.*?if \(!decision\.ShouldPromote \|\| decision\.PromotionIntent is not \{ \} promotion\) return null;.*?new MiracleThreatState\( MiracleInterceptThreatKind\.PostPurifyCrowdControl, promotion\.Target\.GameObjectId, promotion\.Target\.EntityId, promotion\.Target\.JobId, canonical\.Slot, promotion\.ReleasedAtMilliseconds' -or
    $normalizedMiracleIntercept -notmatch 'private static long ThreatLifetime\(MiracleInterceptThreatKind kind\) => kind switch \{ MiracleInterceptThreatKind\.PostPurifyCrowdControl => MiracleProtectionEndRules\.HeldLeaseMilliseconds, MiracleInterceptThreatKind\.PostGuardCrowdControl => MiracleProtectionEndRules\.HeldLeaseMilliseconds, _ => MiracleInterceptRules\.GetThreatLifetimeMilliseconds\(kind\), \}; private static long ThreatLifetime\(MiracleThreatState threat\) => IsProtectionEndThreat\(threat\.Kind\) && threat\.LocalJobId == EnemyCombatConstants\.NinjaJobId \? MiracleProtectionEndRules\.NinjaWeaponskillHeldLeaseMilliseconds : ThreatLifetime\(threat\.Kind\);') {
    throw 'Both exact follow-up states must retire before promotion. The shared dispatcher must measure the strict original release-edge lease: 1,500 ms for WHM/BRD and exactly 3,000 ms for NIN so one 2.5-second Raiju recast plus the existing 500-ms release allowance can complete.'
}
if ($normalizedMiracleIntercept -match 'MiracleInterceptThreatKind\.(?:PostPurifyCrowdControl|PostGuardCrowdControl),.*?decision\.NextState\.LastObservedAtMilliseconds') {
    throw 'Priority-delayed follow-up promotion must never restart its bounded lease from the later framework decision time.'
}
if ($normalizedMiracleIntercept -notmatch 'var selected = SelectFollowupPromotion\(followupPromotions\); activeThreat = selected\.Threat;.*?foreach \(var retired in followupPromotions\).*?if \(retired == selected\) continue;.*?rejectedThreatCount.*?PostPurifyCrowdControl.*?PostGuardCrowdControl.*?guardFollowupRetiredCount' -or
    $normalizedMiracleIntercept -notmatch 'private static int CompareFollowupPromotions\( MiracleFollowupPromotion left, MiracleFollowupPromotion right\).*?MiracleProtectionEndRules\.Compare\(left\.Rank, right\.Rank\)') {
    throw 'Simultaneous Purify/Guard releases must use the one common known-pressure/HP/trusted-MP/stable-identity rank, select one winner, and retire every other same-frame opportunity.'
}
$miraclePriorityBranch = [regex]::Match(
    $normalizedMiracleIntercept,
    'if \(!dispatchAllowed\) \{(?<Body>.*?)\} var currentLocalJobId')
if (-not $miraclePriorityBranch.Success -or
    $miraclePriorityBranch.Groups['Body'].Value -match 'activeThreat\s*=|inputFrame\.Consume|TryUseCounterCcOnce|UseAction|ObservedAtMilliseconds\s*=') {
    throw 'A transient higher-priority helper may wait only: it must not clear/extend the threat, consume/reuse input, or initiate/replay reactive CC.'
}
$miracleResetRuntime = [regex]::Match(
    $normalizedMiracleIntercept,
    'private void ResetRuntime\(\) \{(?<Body>.*?)\} private MiracleInterceptProbeSnapshot WithOpportunityDiagnostics')
if (-not $miracleResetRuntime.Success -or
    $miracleResetRuntime.Groups['Body'].Value -match '\b(recognizedThreatCount|armedThreatCount|rejectedThreatCount|priorityWaitCount|noInputWaitCount|rangeWaitCount|protectionWaitCount|expiredThreatCount|lastOpportunity)\s*=') {
    throw 'Miracle opportunity counters and the last terminal/wait reason must survive ordinary runtime resets for post-match diagnostics.'
}
Assert-Literals $pluginSource @(
    'seen/armed/reject={miracle.RecognizedThreatCount}/{miracle.ArmedThreatCount}/',
    '{miracle.RejectedThreatCount},wait[p/r/k]={miracle.ProtectionWaitCount}/',
    '{miracle.RangeWaitCount}/{miracle.NoInputWaitCount},',
    'priority={miracle.PriorityWaitCount},expired={miracle.ExpiredThreatCount}',
    'last={miracle.LastEvent},last-op={miracle.LastOpportunity}'
) 'Persistent Miracle opportunity diagnostics'
$overlaySource = Read-RequiredSource (Join-Path $sourceRoot 'SeitonSense.Plugin\UI\OverlayRenderer.cs') 'Overlay renderer'
$bullet = [char]0x2022
Assert-Literals $overlaySource @(
    'miracle.ConfirmationPopup is { } miraclePopup && miraclePopup.IsVisible(now)',
    'DrawMiracleInterceptConfirmationCard(',
    '"AUTO CC LANDED"',
    'EnemyCombatConstants.SilentNocturneActionId => "SILENCE"',
    'EnemyCombatConstants.FleetingRaijuActionId => "STUN"',
    '_ => "MIRACLE"',
    ('MiracleInterceptThreatKind.MarksmanSpite => $"{action}  ' + $bullet + '  MCH LB START"'),
    ('MiracleInterceptThreatKind.Zantetsuken => $"{action}  ' + $bullet + '  SAM LB START"'),
    ('MiracleInterceptThreatKind.FuriousBacklash => $"{action}  ' + $bullet + '  VPR NEST START"'),
    ('MiracleInterceptThreatKind.Contradance => $"{action}  ' + $bullet + '  DNC LB START"'),
    'MiracleInterceptThreatKind.PostPurifyCrowdControl =>',
    'MiracleInterceptThreatKind.PostGuardCrowdControl =>',
    ('$"{action}  ' + $bullet + '  AFTER GUARD"'),
    ('$"{action}  ' + $bullet + '  AFTER PURIFY ({PurifyStatusLabel(popup.RemovedStatusId)})"'),
    'MiracleInterceptConfirmationRules.PopupDurationMilliseconds'
) 'Visible, bounded, truthful AUTO CC LANDED news flash'
if ($overlaySource -match '(?i)interrupt(?:ed| successful| confirmed)|cancelled hostile|stopped (?:mch|sam|vpr|dnc|lb|nest)') {
    throw 'AUTO CC LANDED may name the exact counter status and trigger, but may not claim the hostile action was proven interrupted.'
}
Assert-Literals $overlaySource @(
    'var defense = personalStatus.DefensiveUtilityDiagnostics',
    'defense.GuardianPopup is { } acceptedGuardian',
    'acceptedGuardian.IsVisible(now)',
    'heights.Add(GuardianTriggerCardHeight())',
    'BuildCenteredOffsets(heights, 7f * ImGuiHelpers.GlobalScale)',
    'DrawGuardianTriggerCard(',
    'EnemyCombatConstants.GuardianIconId',
    '"GUARDIAN TRIGGERED"',
    ('$"P{popup.PartySlot}  ' + $bullet + '  CLIENT ACCEPTED"')
) 'Shared-stack client-accepted automatic Guardian popup'
$normalizedGuardianOverlay = $overlaySource -replace '\s+', ' '
$guardianWarningStackMethod = [regex]::Match(
    $normalizedGuardianOverlay,
    'private void DrawPersonalWarnings\(long now\) \{(?<Body>.*?)\} private float GuardianTriggerCardHeight')
$guardianCardMethod = [regex]::Match(
    $normalizedGuardianOverlay,
    'private void DrawGuardianTriggerCard\(.*?\) \{(?<Body>.*?)\} private float MiracleInterceptConfirmationCardHeight')
$guardianWarningStackBody = $guardianWarningStackMethod.Groups['Body'].Value
$guardianCardBody = $guardianCardMethod.Groups['Body'].Value
if (-not $guardianWarningStackMethod.Success -or
    -not $guardianCardMethod.Success -or
    [regex]::Matches($overlaySource, '\bDrawGuardianTriggerCard\s*\(').Count -ne 2 -or
    $guardianWarningStackBody -notmatch 'var defense = personalStatus\.DefensiveUtilityDiagnostics;.*?defense\.GuardianPopup is \{ \} acceptedGuardian && acceptedGuardian\.IsVisible\(now\).*?heights\.Add\(GuardianTriggerCardHeight\(\)\);.*?BuildCenteredOffsets\(heights, 7f \* ImGuiHelpers\.GlobalScale\).*?DrawGuardianTriggerCard\( visibleGuardianPopup, stackCenterY \+ offsets\[offsetIndex\], now\);' -or
    $guardianCardBody -notmatch ('EnemyCombatConstants\.GuardianIconId.*?"GUARDIAN TRIGGERED".*?\$"P\{popup\.PartySlot\} ' + [regex]::Escape($bullet) + ' CLIENT ACCEPTED"') -or
    $guardianCardBody -match '(?i)\b(landed|saved|protected)\b' -or
    $guardianCardBody -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|HookFromAddress|ITargetManager|TargetManager|SetTarget|Replay|Retry|Dispatch)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Guardian popup must remain one visual-only card in DrawPersonalWarnings, use the Guardian icon, and state only GUARDIAN TRIGGERED / P# CLIENT ACCEPTED without a server-landed or protection-success claim.'
}

# Smart Warden's Paean is a passive target transform for one already incoming
# action call. Pure Core must preserve vanilla behavior until one exact target
# is selected; runtime drift after selection suppresses that call without an
# original-target fallback, alternate, deferred action, replay, or retry.
$smartWardensPaeanRules = Read-RequiredSource $smartWardensPaeanRulesPath 'Smart Warden''s Paean target rules'
$normalizedSmartWardensPaeanRules = $smartWardensPaeanRules -replace '\s+', ' '
$smartWardensPaeanService = Read-RequiredSource $smartWardensPaeanServicePath 'Smart Warden''s Paean runtime service'
$normalizedSmartWardensPaeanService = $smartWardensPaeanService -replace '\s+', ' '
$smartWardensPaeanSelfTests = Read-RequiredSource $smartWardensPaeanSelfTestsPath 'Smart Warden''s Paean self-tests'
$smartPaeanPressureTracker = Read-RequiredSource $targetPressureTrackerPath 'Smart Paean pressure source'
$normalizedSmartPaeanPressureTracker = $smartPaeanPressureTracker -replace '\s+', ' '

Assert-Literals $smartWardensPaeanRules @(
    'public readonly record struct SmartWardensPaeanCandidate(',
    'bool HasWardensPaeanWard,',
    'bool PressureKnown,',
    'int UniqueIncomingEnemyCount);',
    'public readonly record struct SmartWardensPaeanIntent(',
    'public readonly record struct SmartWardensPaeanObservation(',
    'bool CompleteExactPartyView,',
    'Vanilla = 0,',
    'Redirect = 1,',
    'public const uint BardJobId = 23;',
    'public const uint ActionId = 29_400;',
    'public const uint WardensPaeanWardStatusId = 3_143;',
    'public const int MinimumIncomingEnemyCount = 3;',
    'public const int RequiredCrystallineConflictPartySize = 5;',
    'public const int FirstPartySlot = 1;',
    'public const int LastPartySlot = 8;',
    'new HashSet<int>()',
    'new HashSet<ulong>()',
    'new HashSet<uint>()',
    '!occupiedSlots.Add(candidate.PartySlot)',
    '!occupiedGameObjectIds.Add(candidate.Actor.GameObjectId)',
    '!occupiedEntityIds.Add(candidate.Actor.EntityId)',
    'return localEntries == 1;',
    '!candidate.HasWardensPaeanWard',
    'candidate.PressureKnown',
    'candidate.UniqueIncomingEnemyCount >= MinimumIncomingEnemyCount',
    'right.UniqueIncomingEnemyCount.CompareTo(',
    'left.UniqueIncomingEnemyCount);',
    'left.PartySlot.CompareTo(right.PartySlot)',
    'left.Actor.EntityId.CompareTo(right.Actor.EntityId)',
    'left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId)',
    'currentCandidate.PartySlot == intent.PartySlot',
    'currentCandidate.Actor == intent.Target',
    'IsEligibleCandidate(currentCandidate, currentLocalPlayer)'
) 'Pure Smart Warden''s Paean exact-pressure target policy'
if ($normalizedSmartWardensPaeanRules -notmatch 'if \(!observation\.ConfigurationEnabled\).*?ConfigurationDisabled.*?if \(!observation\.IsCrystallineConflict\).*?OutsideCrystallineConflict.*?if \(!observation\.LocalPlayer\.IsValid\).*?LocalPlayerIdentityInvalid.*?if \(!observation\.IsLocalPlayerAlive\).*?LocalPlayerDead.*?if \(observation\.LocalJobId != BardJobId\).*?LocalJobInvalid.*?if \(!observation\.MetadataVerified\).*?MetadataUnverified.*?if \(observation\.ResolvedActionId != ActionId\).*?ResolvedActionInvalid' -or
    $normalizedSmartWardensPaeanRules -notmatch 'candidates\.Count != RequiredCrystallineConflictPartySize.*?candidate\.PartySlot is < FirstPartySlot or > LastPartySlot.*?!candidate\.ExactPartyIdentity.*?!candidate\.Actor\.IsValid.*?candidate\.IsSelf != isExactLocal.*?!occupiedSlots\.Add\(candidate\.PartySlot\).*?!occupiedGameObjectIds\.Add\(candidate\.Actor\.GameObjectId\).*?!occupiedEntityIds\.Add\(candidate\.Actor\.EntityId\).*?return localEntries == 1;' -or
    $normalizedSmartWardensPaeanRules -notmatch 'candidate\.Actor != localPlayer.*?!candidate\.IsSelf.*?candidate\.Alive.*?candidate\.Targetable.*?!candidate\.HasWardensPaeanWard.*?candidate\.CurrentHp > 0.*?candidate\.MaximumHp > 0.*?candidate\.CurrentHp <= candidate\.MaximumHp.*?candidate\.NativeTargetValid.*?candidate\.NativeRangeAndLineOfSight.*?candidate\.PressureKnown.*?candidate\.UniqueIncomingEnemyCount >= MinimumIncomingEnemyCount') {
    throw 'Smart Paean Core must require default-off exact CC/BRD/action metadata, one complete unique exact five-member party view, a non-self live target without ward 3143, native reachability, and known pressure of at least three.'
}
if ($normalizedSmartWardensPaeanRules -notmatch 'var pressure = right\.UniqueIncomingEnemyCount\.CompareTo\( left\.UniqueIncomingEnemyCount\); if \(pressure != 0\) return pressure; var health = \(\(ulong\)left\.CurrentHp \* right\.MaximumHp\)\.CompareTo\( \(ulong\)right\.CurrentHp \* left\.MaximumHp\); if \(health != 0\) return health; var partySlot = left\.PartySlot\.CompareTo\(right\.PartySlot\); if \(partySlot != 0\) return partySlot; var entityId = left\.Actor\.EntityId\.CompareTo\(right\.Actor\.EntityId\); return entityId != 0 \? entityId : left\.Actor\.GameObjectId\.CompareTo\(right\.Actor\.GameObjectId\);' -or
    $normalizedSmartWardensPaeanRules -notmatch 'var selectedIndex = SelectBestCandidateIndex\( observation\.Candidates, observation\.LocalPlayer\); if \(selectedIndex < 0\).*?NoKnownPressureTarget.*?var candidate = observation\.Candidates!\[selectedIndex\]; var intent = new SmartWardensPaeanIntent\( observation\.ResolvedActionId, candidate\.PartySlot, observation\.LocalPlayer, candidate\.Actor, candidate\.UniqueIncomingEnemyCount\);') {
    throw 'Smart Paean must rank pressure descending, exact HP ratio ascending, then P-slot/EID/GOID, and freeze exactly that one selected actor intent.'
}
$smartPaeanFinalIntentMethod = [regex]::Match(
    $normalizedSmartWardensPaeanRules,
    'public static bool CanUseFrozenIntent\(.*?\) =>(?<Body>.*?); private static SmartWardensPaeanDecisionReason GetGateFailure')
if (-not $smartPaeanFinalIntentMethod.Success -or
    $smartPaeanFinalIntentMethod.Groups['Body'].Value -notmatch 'intent\.IsValid.*?configurationEnabled.*?isCrystallineConflict.*?currentLocalJobId == BardJobId.*?currentLocalPlayer == intent\.LocalPlayer.*?isLocalPlayerAlive.*?metadataVerified.*?resolvedActionId == intent\.ActionId.*?currentCandidate\.PartySlot == intent\.PartySlot.*?currentCandidate\.Actor == intent\.Target.*?IsEligibleCandidate\(currentCandidate, currentLocalPlayer\)' -or
    $smartWardensPaeanRules -match '\b(ActionLocallyReady|OffCooldown|CooldownRemaining|GetRecastTime|UseAction|UseActionLocation|ExecuteAction|SendAction|HookFromAddress|ITargetManager|TargetManager|SetTarget|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Smart Paean frozen-intent validation must recheck only the same exact action/local/P-slot/actor eligibility, including live pressure and ward, without a cooldown gate, dispatch, target mutation, scheduling, or retry state.'
}

$smartPaeanSelfTestMethods = @(
    'EligibilityRequiresKnownPressureAndNativeReachability',
    'CompletePartyViewRejectsIdentityAmbiguity',
    'RankingIsPressureThenExactHpThenStableSlot',
    'UnknownOrMissingPressurePreservesVanillaCall',
    'FrozenIntentCannotRerankFallbackOrRetry'
)
foreach ($method in $smartPaeanSelfTestMethods) {
    Assert-Literals $smartWardensPaeanSelfTests @("internal static void $method()") "Smart Paean self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("SmartWardensPaeanTargetSelfTests.$method") "Smart Paean test registration $method"
}
Assert-Literals $smartWardensPaeanSelfTests @(
    'IsWardensPaeanWardStatus(3_143)',
    'IsWardensPaeanWardStatus(2_178)',
    'UniqueIncomingEnemyCount = 2',
    'PressureKnown = false',
    'HasWardensPaeanWard = true',
    'NativeRangeAndLineOfSight = false',
    '"33/100 is exactly lower than 1/3"',
    'SmartWardensPaeanDecisionKind.Vanilla',
    '"a now-better alternate cannot replace the frozen actor"',
    '"pressure became unknown"',
    '"pressure fell below threshold"',
    '"PvP ward appeared before dispatch"'
) 'Smart Paean positive, vanilla, ward, pressure, ranking, and frozen-intent tests'
if ([regex]::Matches($smartWardensPaeanSelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 5 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bSmartWardensPaeanTargetSelfTests\.\w+').Count -ne 5) {
    throw 'All five Smart Paean eligibility, exact-party, ranking, vanilla, and frozen-intent self-tests must remain registered exactly once.'
}

Assert-Literals $smartWardensPaeanService @(
    'internal sealed unsafe class SmartWardensPaeanService',
    'internal const uint WardensPaeanIconId = 9_628;',
    'internal const uint WardensPaeanWardIconId = 212_611;',
    'private const ushort ExpectedRecast100ms = 240;',
    'private const int ExpectedRange = 30;',
    'ResolveActionId(actionManager, actionType, rawActionId)',
    'SmartWardensPaeanTargetRules.ActionId',
    'SmartWardensPaeanTargetRules.WardensPaeanWardStatusId',
    'configuration.EnableBardWardensPaeanPressureRedirect',
    'ResolveContext() == SupportedPvPContext.CrystallineConflict',
    'localJobId == SmartWardensPaeanTargetRules.BardJobId',
    'CaptureExactParty(localPlayer!)',
    'pressureTracker.TryCaptureIncomingAllyPressure(',
    'PartySlotResolver.Resolve(objectTable, intent.PartySlot)',
    'SmartWardensPaeanTargetRules.CanUseFrozenIntent(',
    'redirectCommitted = true;',
    '? RecordSuppressed(',
    ': RecordVanilla(',
    'HasLiveGuard(currentLocal)',
    'HasLiveWardensPaeanWard(target)',
    'ActionManager.GetActionInRangeOrLoS(',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult)',
    'status.RemainingTime > 0f',
    'Client accepted redirected Paean',
    'Client rejected redirected Paean'
) 'Passive exact Smart Paean runtime and truthful client-return diagnostics'
$smartPaeanEvaluateMethod = [regex]::Match(
    $normalizedSmartWardensPaeanService,
    'internal SmartWardensPaeanInterceptResult Evaluate\(.*?\) \{(?<Body>.*?)\} internal void RecordNativeResult')
$smartPaeanCaptureMethod = [regex]::Match(
    $normalizedSmartWardensPaeanService,
    'private ExactPartyCapture CaptureExactParty\(.*?\) \{(?<Body>.*?)\} private bool PartySlotsRemainExact')
$smartPaeanBuildCandidateMethod = [regex]::Match(
    $normalizedSmartWardensPaeanService,
    'private RuntimeCandidate BuildCandidate\(.*?\) \{(?<Body>.*?)\} private HashSet<uint>\? CaptureExactPartyEntityIds')
$smartPaeanEvaluateBody = $smartPaeanEvaluateMethod.Groups['Body'].Value
$smartPaeanCaptureBody = $smartPaeanCaptureMethod.Groups['Body'].Value
$smartPaeanBuildCandidateBody = $smartPaeanBuildCandidateMethod.Groups['Body'].Value
if (-not $smartPaeanEvaluateMethod.Success -or
    -not $smartPaeanCaptureMethod.Success -or
    -not $smartPaeanBuildCandidateMethod.Success -or
    $smartPaeanEvaluateBody -notmatch 'if \(resolvedActionId != SmartWardensPaeanTargetRules\.ActionId\).*?Vanilla.*?if \(!IsRecognizedInvocation\(actionType, mode\)\).*?RecordVanilla.*?if \(localGuardActiveOrPropagating\).*?RecordVanilla' -or
    $smartPaeanEvaluateBody -notmatch 'var decision = SmartWardensPaeanTargetRules\.Observe\(.*?if \(!decision\.ShouldRedirect.*?RecordVanilla.*?redirectCommitted = true;.*?currentTarget = PartySlotResolver\.Resolve\(objectTable, intent\.PartySlot\).*?currentTargetIdentity != intent\.Target.*?RecordSuppressed.*?CanUseFrozenIntent\(.*?RecordSuppressed.*?RecordRedirect\(' -or
    $smartPaeanEvaluateBody -notmatch 'catch \(Exception exception\).*?return redirectCommitted \? RecordSuppressed\(.*?: RecordVanilla\(') {
    throw 'Smart Paean must keep every preselection failure vanilla, commit one frozen target only after Core redirect selection, then suppress every exact-final-preflight failure or exception without fallback.'
}
if ($smartPaeanCaptureBody -notmatch 'var partyBefore = CaptureExactPartyEntityIds\(\).*?pressureTracker\.TryCaptureIncomingAllyPressure\( out var pressureCounts\).*?for \(var slot = SmartWardensPaeanTargetRules\.FirstPartySlot; slot <= SmartWardensPaeanTargetRules\.LastPartySlot; slot\+\+\).*?BuildCandidate\( localPlayer, player!, slot, pressureViewActive, pressureCounts\).*?var partyAfter = CaptureExactPartyEntityIds\(\).*?partyBefore\.SetEquals\(partyAfter\).*?members\.Count == SmartWardensPaeanTargetRules\.RequiredCrystallineConflictPartySize.*?SetEquals\(partyBefore\).*?Distinct\(\)\.Count\(\) == members\.Count.*?PartySlotsRemainExact\(members\)' -or
    $smartPaeanBuildCandidateBody -notmatch 'GetNativeObject\(localPlayer\).*?GetNativeObject\(target\).*?ActionManager\.GetActionInRangeOrLoS\( SmartWardensPaeanTargetRules\.ActionId, sourceObject, targetObject\).*?pressureViewActive && pressureCounts\.TryGetValue\( targetIdentity, out incomingEnemyCount\) && incomingEnemyCount >= 0.*?HasLiveWardensPaeanWard\(target\).*?SeitonRangeRules\.HasNativeRangeAndLineOfSight\(rangeResult\).*?pressureKnown, incomingEnemyCount') {
    throw 'Smart Paean selection must capture one immutable pressure view for one stable complete five-member P1-P8 party set and build every candidate from exact IDs/address, live ward, native 30y/LoS, and non-negative pressure in that same view.'
}
if ($normalizedSmartWardensPaeanService -notmatch 'private bool PartySlotsRemainExact\(.*?PartySlotResolver\.Resolve\( objectTable, member\.Candidate\.PartySlot\).*?stableIdentity != member\.Candidate\.Actor.*?stablePlayer!\.Address != member\.Address.*?return false;' -or
    $normalizedSmartWardensPaeanService -notmatch 'private HashSet<uint>\? CaptureExactPartyEntityIds\(\).*?ids\.Length != SmartWardensPaeanTargetRules\.RequiredCrystallineConflictPartySize.*?ids\.Any\(static entityId => !IsNetworkEntityId\(entityId\)\).*?unique\.Count == ids\.Length \? unique : null' -or
    $normalizedSmartWardensPaeanService -notmatch 'private bool TryGetExactIdentity\(.*?player\.Address == nint\.Zero.*?!IsNetworkObjectId\(player\.GameObjectId\).*?!IsNetworkEntityId\(player\.EntityId\).*?native->EntityId != player\.EntityId.*?tablePlayer\.Address != player\.Address.*?tablePlayer\.GameObjectId != player\.GameObjectId.*?tablePlayer\.EntityId != player\.EntityId') {
    throw 'Smart Paean exact-party capture must reject incomplete, duplicate, changed, or object-table/native identity mismatches before a redirect can be committed.'
}
if ($normalizedSmartWardensPaeanService -notmatch 'action\.Name\.ToString\(\), "The Warden''s Paean".*?action\.Icon == WardensPaeanIconId.*?action\.IsPvP.*?action\.IsPlayerAction.*?action\.ClassJob\.RowId == SmartWardensPaeanTargetRules\.BardJobId.*?action\.Range == ExpectedRange.*?action\.EffectRange == 0.*?action\.Cast100ms == 0.*?action\.Recast100ms == ExpectedRecast100ms.*?action\.CanTargetSelf.*?action\.CanTargetParty.*?!action\.CanTargetAlly.*?!action\.CanTargetAlliance.*?!action\.CanTargetHostile.*?!action\.TargetArea.*?action\.RequiresLineOfSight.*?actionDescription\.Contains\("Removes".*?actionDescription\.Contains\("Purify".*?actionDescription\.Contains\("barrier"' -or
    $normalizedSmartWardensPaeanService -notmatch 'ward\.Name\.ToString\(\), "The Warden''s Paean".*?ward\.Icon == WardensPaeanWardIconId.*?ward\.StatusCategory == 1.*?wardDescription\.Contains\("Purify"' -or
    $normalizedSmartWardensPaeanService -notmatch 'SmartWardensPaeanTargetRules\.IsWardensPaeanWardStatus\(status\.StatusId\).*?float\.IsFinite\(status\.RemainingTime\).*?status\.RemainingTime > 0f') {
    throw 'Smart Paean metadata must fail closed on exact PvP BRD action 29400 (icon 9628, 30y, LoS, target flags and description) and exact live ward status 3143 (icon 212611/category/description).'
}
if ($normalizedSmartWardensPaeanService -notmatch 'private static bool IsRecognizedInvocation\(.*?actionType is ActionType\.Action or ActionType\.PvPAction.*?mode is ActionManager\.UseActionMode\.None or ActionManager\.UseActionMode\.Macro or ActionManager\.UseActionMode\.Queue.*?\(uint\)mode == 100' -or
    ($smartWardensPaeanRules + $smartWardensPaeanService + $smartWardensPaeanSelfTests) -match '\b(ActionLocallyReady|OffCooldown|CooldownRemaining|GetRecastTimeElapsed|GetRecastTimeRemaining)\b' -or
    $smartWardensPaeanService -match '(?:->|\.)UseAction\s*\(|\b(Hook<|HookFromAddress|ITargetManager|TargetManager|SetTarget|UseActionLocation|ExecuteAction|SendAction|ActionQueued|QueuedAction|QueueAction|RetryAction|RetryDispatch|PendingDispatch|BufferedDispatch)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Smart Paean must review normal/PvP manual, macro, queue, and Turbo-mode incoming calls without any cooldown gate, hook, action initiation, target mutation, buffer, alternate, or retry of its own.'
}
if ($normalizedSmartPaeanPressureTracker -notmatch 'internal bool TryCaptureIncomingAllyPressure\( out IReadOnlyDictionary<TargetPressureActorIdentity, int> counts, out long publishedAtMilliseconds\) \{ var current = Volatile\.Read\(ref incomingAllyPressure\); counts = current\.Counts; publishedAtMilliseconds = current\.PublishedAtMilliseconds; return current\.Active; \}' -or
    $normalizedSmartPaeanPressureTracker -notmatch 'incomingAllyPressureEnabledForContext = supportedContext == SupportedPvPContext\.CrystallineConflict &&.*?configuration\.EnableBardWardensPaeanPressureRedirect' -or
    $normalizedSmartPaeanPressureTracker -notmatch 'new IncomingAllyPressureRuntimeState\( pressureStateTrackingEnabled, publishedAtMilliseconds, core\.IncomingAllyPressure\.ToDictionary\( static pressure => pressure\.Ally, static pressure => pressure\.UniqueEnemyCount\)\); Interlocked\.Exchange\(ref incomingAllyPressure, publishedIncomingAllyPressure\);') {
    throw 'Smart Paean pressure must activate independently in exact CC and expose one atomically published immutable exact-identity/count view per selection or frozen-target revalidation.'
}

$smartPaeanTypeReferences = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bSmartWardensPaeanService\b')
$unexpectedSmartPaeanReferences = @($smartPaeanTypeReferences | Where-Object {
    $_.Path -notin @($pluginPath, $nearAssistPath, $smartWardensPaeanServicePath)
})
if ($unexpectedSmartPaeanReferences.Count -gt 0 -or
    [regex]::Matches($pluginSource, '\bnew\s+SmartWardensPaeanService\s*\(').Count -ne 1) {
    $locations = $unexpectedSmartPaeanReferences | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Smart Paean may be constructed once and consulted only by the existing shared action detour: $($locations -join ', ')"
}

# The NIN Seiton helper is a separate default-off held-level action boundary.
# Pure rules select exactly one canonical CC enemy by exact HP ratio. Runtime
# claims only one scheduler frame per native boundary, retries only a proven
# clean false, and opens one follow-up epoch only after an accepted base action.
$ninjaSeitonRules = Read-RequiredSource $ninjaSeitonDispatchRulesPath 'NIN Seiton dispatch rules'
$ninjaSeiton = Read-RequiredSource $ninjaSeitonProbePath 'NIN Seiton dispatch runtime'
$normalizedNinjaSeitonRules = $ninjaSeitonRules -replace '\s+', ' '
$normalizedNinjaSeiton = $ninjaSeiton -replace '\s+', ' '
Assert-Literals $ninjaSeitonRules @(
    'BaseActionId = 29_515',
    'FollowUpActionId = 29_516',
    'IReadOnlyList<NinjaSeitonDispatchCandidate>? Candidates',
    'NinjaSeitonAcceptedHoldState(',
    'BeginAcceptedHold(',
    'ObserveAcceptedHold(',
    'CanOpenAdjustedActionEpoch(',
    'RetireAdjustedActionEpoch(',
    'FollowUpEpochSpent',
    'HeldGameplayKeyEligible',
    'ActionHelpersSuppressedByGuard',
    'HigherPriorityClaimed',
    'ExactCanonicalIdentity',
    'ExecuteBlockingStatusId',
    'HasExecuteBlockingProtection',
    'CoveredLegacyStatusId = 81',
    'CoveredStatusId = 1_301',
    'CoveredPvpStatusId = 2_413',
    'CoveredPvpAlternateStatusId = 4_352',
    'HallowedGroundStatusId = 1_302',
    'UndeadRedemptionStatusId = 3_039',
    '!candidate.HasExecuteBlockingProtection',
    'ExecuteThreshold.IsBelowHalf',
    'HasValidActionTarget',
    'HasNativeRangeAndLineOfSight',
    'SelectBestCandidateIndex(',
    'new HashSet<int>()',
    'new HashSet<TargetPressureActorIdentity>()',
    '!occupiedSlots.Add(candidate.EnemySlot)',
    '!occupiedActors.Add(candidate.Actor)',
    'CompareRatio(',
    'left.EnemySlot.CompareTo(right.EnemySlot)',
    'left.Actor.EntityId.CompareTo(right.Actor.EntityId)',
    'left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId)',
    'public bool ShouldConsumeInputGeneration => ShouldDispatch',
    'CanUseExactIntent(',
    'candidate.EnemySlot == intent.EnemySlot',
    'candidate.Actor == intent.Target',
    'selector again after consuming input; drift simply cancels the attempt'
) 'Deterministic exact NIN Seiton dispatch policy'
if ($normalizedNinjaSeitonRules -notmatch 'if \(!observation\.ConfigurationEnabled\).*?ConfigurationDisabled.*?if \(!observation\.IsCrystallineConflict\).*?OutsideCrystallineConflict.*?if \(!observation\.LocalPlayer\.IsValid\).*?LocalPlayerIdentityInvalid.*?if \(!observation\.IsLocalPlayerAlive\).*?LocalPlayerDead.*?if \(!ExecuteThreshold\.IsNinja\(observation\.LocalJobId\)\).*?LocalJobInvalid.*?if \(!observation\.MetadataVerified\).*?MetadataUnverified.*?if \(observation\.ActionHelpersSuppressedByGuard\).*?GuardSuppressed.*?if \(observation\.HigherPriorityClaimed\).*?HigherPriorityClaimed.*?if \(!observation\.InputProbeSucceeded\).*?InputProbeUnavailable.*?if \(observation\.IsTextInputActive\).*?TextInputActive.*?if \(!observation\.HeldGameplayKeyEligible\).*?NoHeldGameplayKey.*?if \(!IsExactSeitonAction\(observation\.ResolvedActionId\)\).*?ResolvedActionInvalid.*?if \(!observation\.ActionLocallyReady\).*?ActionNotReady') {
    throw 'NIN Seiton policy must require default-off enablement, exact CC/NIN/local identity, verified metadata, no Guard or higher claim, one exact held non-text key epoch, and exact ready 29515/29516.'
}
if ($normalizedNinjaSeitonRules -notmatch 'candidate\.Actor != localPlayer.*?EnemySlotRules\.IsValidSlot\(candidate\.EnemySlot\).*?candidate\.ExactCanonicalIdentity.*?candidate\.Alive.*?candidate\.Targetable.*?ExecuteThreshold\.IsBelowHalf\(candidate\.CurrentHp, candidate\.MaximumHp\).*?!candidate\.HasExecuteBlockingProtection.*?candidate\.HasValidActionTarget.*?candidate\.HasNativeRangeAndLineOfSight' -or
    $normalizedNinjaSeitonRules -notmatch 'if \(!occupiedSlots\.Add\(candidate\.EnemySlot\) \|\| !occupiedActors\.Add\(candidate\.Actor\)\) \{ return -1; \}.*?if \(bestIndex < 0 \|\| Compare\(candidate, candidates\[bestIndex\]\) < 0\) bestIndex = index;' -or
    $normalizedNinjaSeitonRules -notmatch '\(\(ulong\)leftCurrent \* rightMaximum\)\.CompareTo\( \(ulong\)rightCurrent \* leftMaximum\)') {
    throw 'NIN Seiton selection must fail closed on duplicate exact slots/actors and rank only eligible sub-50 targets by overflow-safe exact HP ratio.'
}
if ($normalizedNinjaSeitonRules -notmatch 'public static bool IsExecuteBlockingStatus\(uint statusId\) => statusId is CoveredLegacyStatusId or CoveredStatusId or CoveredPvpStatusId or CoveredPvpAlternateStatusId or HallowedGroundStatusId or UndeadRedemptionStatusId;' -or
    [regex]::Matches($normalizedNinjaSeitonRules, 'StatusId = ').Count -ne 6) {
    throw 'NIN Seiton protection metadata must remain the exact six target-side Covered, Hallowed Ground, and Undead Redemption rows.'
}
if ($normalizedNinjaSeitonRules -notmatch 'var health = CompareRatio\( left\.CurrentHp, left\.MaximumHp, right\.CurrentHp, right\.MaximumHp\); if \(health != 0\) return health; var slot = left\.EnemySlot\.CompareTo\(right\.EnemySlot\); if \(slot != 0\) return slot; var entity = left\.Actor\.EntityId\.CompareTo\(right\.Actor\.EntityId\); return entity != 0 \? entity : left\.Actor\.GameObjectId\.CompareTo\(right\.Actor\.GameObjectId\);' -or
    $normalizedNinjaSeitonRules -notmatch 'intent\.IsValid && actionLocallyReady && resolvedActionId == intent\.ActionId && candidate\.EnemySlot == intent\.EnemySlot && candidate\.Actor == intent\.Target && IsEligibleCandidate\(candidate, localPlayer\)') {
    throw 'NIN Seiton must use ratio, S-slot, EntityId, and GameObjectId ordering, then validate only the frozen action/slot/actor intent.'
}
if ($ninjaSeitonRules -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|ResolvePlaceholder|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b' -or
    $ninjaSeitonRules -cmatch '\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction|PendingDispatch|BufferedDispatch)\b') {
    throw 'Pure NIN Seiton rules may model held epochs but must never dispatch, observe time, buffer, queue, mutate, or depend on the visible target.'
}

if ([regex]::Matches($ninjaSeiton, '(?:->|\.)UseAction\s*\(').Count -ne 1) {
    throw 'NIN Seiton runtime must contain exactly one native UseAction boundary.'
}
Assert-Literals $ninjaSeiton @(
    'NinjaSeitonDispatchProbeSnapshot(',
    'UseActionAttempted',
    'UseActionAccepted',
    'RevalidatedCurrentHp',
    'RevalidatedMaximumHp',
    'ExecuteBlockingStatusId',
    'BoundaryThresholdRevalidated',
    'ThresholdDriftCancelled',
    'ProtectionDriftCancelled',
    'ThresholdDriftCancellationCount',
    'ProtectionDriftCancellationCount',
    'CandidateCount',
    'CandidateResolution',
    'executeTracker.Diagnostics',
    'diagnosticsBefore.IsCrystallineConflict',
    'diagnosticsBefore.SeitonMetadataVerified',
    'executeTracker.Enemies.ToArray()',
    'seenSlots.Add(snapshotEnemy.Slot)',
    'seenGameObjectIds.Add(snapshotEnemy.GameObjectId)',
    'seenEntityIds.Add(snapshotEnemy.EntityId)',
    'diagnosticsBefore.SlotCapacity != EnemySlotRules.LastSlot',
    'diagnosticsBefore.ResolvedSlots != EnemySlotRules.LastSlot',
    'ReferenceEquals(diagnosticsBefore, diagnosticsAfter)',
    'snapshots.Length != diagnosticsBefore.ValidEnemySlots',
    'new Dictionary<int, EnemyHudSnapshot>(snapshots.Length)',
    'for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)',
    'new HashSet<nint>()',
    'eligibleCurrentSlots.Length != diagnosticsBefore.ValidEnemySlots',
    'snapshotsBySlot.TryGetValue(slot, out var snapshotEnemy)',
    'Native S{slot} changed during capture',
    'EnemySlotResolver.Resolve(objectTable, enemySlot)',
    'objectTable.SearchByEntityId(target.EntityId)',
    'SeitonReadinessProbe.TryGetReadyAction(',
    'SeitonReadinessProbe.HasRangeAndLineOfSight(',
    'inputFrame.HeldGameplayKeyEligible',
    'NinjaSeitonDispatchRules.ObserveAcceptedHold(',
    'NinjaSeitonDispatchRules.CanOpenAdjustedActionEpoch(',
    'NinjaSeitonDispatchRules.Observe(',
    'inputFrame.Consume()',
    'HeldActionRetryRules.RetainsSchedulerFrame(',
    'HeldActionRetryRules.CanAttemptFrozenIntent(',
    'ResolveFrozenIntent(',
    'NinjaSeitonDispatchRules.CanUseExactIntent(',
    'var outcome = TryUseSeitonOnce(',
    'ReadFrozenThresholdAtUseActionBoundary(',
    'NinjaSeitonProtectionProbe.TryFindExecuteBlockingStatus(',
    'BoundaryThresholdResult.Protected',
    'BoundaryThresholdResult.AtOrAboveHalf',
    'thresholdResult != BoundaryThresholdResult.BelowHalf',
    'thresholdRevalidatedAtBoundary = true;',
    'attemptedAtBoundary = true;',
    'nearAssist.RunWithoutRedirect',
    'ClientActionAttemptBoundary.Capture(',
    'ClientActionAttemptBoundaryRules.Classify(',
    'HeldActionRetryRules.Complete(',
    'HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(',
    'NinjaSeitonDispatchRules.BeginAcceptedHold(',
    'NinjaSeitonDispatchRules.RetireAdjustedActionEpoch(',
    'ActionType.Action',
    'ActionManager.UseActionMode.None',
    'HeldActionRetryRules.MaximumNativeAttempts'
) 'Exact held-epoch NIN Seiton runtime, shared retry boundary, and truthful diagnostics'
if ($normalizedNinjaSeiton -notmatch 'var featureContextReady = configurationEnabled && isCrystallineConflict && localAlive && ExecuteThreshold\.IsNinja\(localJobId\) && metadataVerified && !actionHelpersSuppressedByGuard && !hardReset; var resolvedActionId = 0u; var actionLocallyReady = featureContextReady && localIdentity\.IsValid && SeitonReadinessProbe\.TryGetReadyAction\(localPlayer!, out resolvedActionId\) && IsActionResourceReady\(resolvedActionId\); var nearQueueable = actionLocallyReady && IsNativeBoundaryNearQueueable\(localPlayer!\);' -or
    $normalizedNinjaSeiton -notmatch 'acceptedHold = NinjaSeitonDispatchRules\.ObserveAcceptedHold\(.*?var hasHeldEpoch = acceptedHold\.OwnsHold \? NinjaSeitonDispatchRules\.CanOpenAdjustedActionEpoch\( acceptedHold, resolvedActionId\) : inputFrame\.HeldGameplayKeyEligible; var shouldResolveCandidates = frozenRetry is null && terminalHeldKey == VirtualKey\.NO_KEY && actionLocallyReady && !higherPriorityClaimed && input\.ProbeSucceeded && !input\.IsTextInputActive && hasHeldEpoch;.*?var candidates = shouldResolveCandidates \? ResolveExactCandidates\(localPlayer!, resolvedActionId, out candidateResolution\) : \[\];.*?hasHeldEpoch, resolvedActionId, actionLocallyReady, candidates, hardReset') {
    throw 'NIN Seiton may capture candidates only behind exact CC/NIN/metadata/Guard/readiness gates and one unclaimed exact held action epoch.'
}
if ($normalizedNinjaSeiton -notmatch 'var diagnosticsBefore = executeTracker\.Diagnostics; if \(!diagnosticsBefore\.Active \|\| !diagnosticsBefore\.IsCrystallineConflict \|\| !diagnosticsBefore\.SeitonMetadataVerified\).*?if \(diagnosticsBefore\.SlotCapacity != EnemySlotRules\.LastSlot \|\| diagnosticsBefore\.ResolvedSlots != EnemySlotRules\.LastSlot\).*?var snapshots = executeTracker\.Enemies\.ToArray\(\); var diagnosticsAfter = executeTracker\.Diagnostics; if \(!ReferenceEquals\(diagnosticsBefore, diagnosticsAfter\)\).*?if \(snapshots\.Length > EnemySlotRules\.LastSlot \|\| snapshots\.Length != diagnosticsBefore\.ValidEnemySlots\)' -or
    $normalizedNinjaSeiton -notmatch 'foreach \(var snapshotEnemy in snapshots\).*?!seenSlots\.Add\(snapshotEnemy\.Slot\).*?!seenGameObjectIds\.Add\(snapshotEnemy\.GameObjectId\).*?!seenEntityIds\.Add\(snapshotEnemy\.EntityId\).*?return \[\];.*?snapshotsBySlot\.Add\(snapshotEnemy\.Slot, snapshotEnemy\);') {
    throw 'NIN Seiton must require a stable complete five-slot tracker frame with a duplicate-free valid-enemy snapshot before considering a target.'
}
if ($normalizedNinjaSeiton -notmatch 'for \(var slot = EnemySlotRules\.FirstSlot; slot <= EnemySlotRules\.LastSlot; slot\+\+\).*?EnemySlotResolver\.Resolve\(objectTable, slot\).*?objectTable\.SearchByEntityId\(player!\.EntityId\) as IPlayerCharacter.*?tablePlayer\.Address != player\.Address.*?tablePlayer\.GameObjectId != player\.GameObjectId.*?tablePlayer\.EntityId != player\.EntityId.*?!seenGameObjectIds\.Add\(player\.GameObjectId\).*?!seenEntityIds\.Add\(player\.EntityId\).*?!seenAddresses\.Add\(player\.Address\).*?return \[\];.*?currentSlots\.Add\(\(slot, player\)\);') {
    throw 'NIN Seiton must resolve all native e1-e5 slots to unique object-table address/GOID/EID identities; any missing, mismatched, or duplicate identity aborts the full set.'
}
if ($normalizedNinjaSeiton -notmatch 'var eligibleCurrentSlots = currentSlots \.Where\(static entry => IsLivePlayer\(entry\.Player\) && entry\.Player\.IsTargetable && ExecuteThreshold\.HasValidHp\(entry\.Player\.CurrentHp, entry\.Player\.MaxHp\)\) \.ToArray\(\); if \(eligibleCurrentSlots\.Length != diagnosticsBefore\.ValidEnemySlots \|\| eligibleCurrentSlots\.Length != snapshots\.Length\).*?foreach \(var \(slot, player\) in eligibleCurrentSlots\).*?!snapshotsBySlot\.TryGetValue\(slot, out var snapshotEnemy\).*?snapshotEnemy\.GameObjectId != player\.GameObjectId.*?snapshotEnemy\.EntityId != player\.EntityId.*?return \[\];.*?var candidate = BuildExactSlotCandidate\( localPlayer, actionId, slot, expectedTarget\); if \(candidate is not \{ \} exact\).*?return \[\];.*?candidates\.Add\(exact\);') {
    throw 'Every current live, targetable, valid-HP enemy must exactly match one tracker slot and pass native action validation; incomplete or stale eligible sets must fail closed.'
}
if ($normalizedNinjaSeiton -notmatch 'foreach \(var \(slot, player\) in currentSlots\).*?var stablePlayer = EnemySlotResolver\.Resolve\(objectTable, slot\); if \(!HasValidNativeIdentity\(stablePlayer\) \|\| stablePlayer!\.Address != player\.Address \|\| stablePlayer\.GameObjectId != player\.GameObjectId \|\| stablePlayer\.EntityId != player\.EntityId\).*?return \[\];.*?var protectedCandidates = candidates\.Count\( static candidate => candidate\.HasExecuteBlockingProtection\); resolution = \$"Exact coherent set: \{candidates\.Count\} candidates, protected=\{protectedCandidates\}"; return candidates;') {
    throw 'NIN Seiton must re-resolve the complete native e1-e5 identity set unchanged before returning any ranked candidates.'
}
if ($normalizedNinjaSeiton -notmatch 'var target = EnemySlotResolver\.Resolve\(objectTable, enemySlot\); if \(!HasValidNativeIdentity\(target\) \|\| target!\.GameObjectId != expectedTarget\.GameObjectId \|\| target\.EntityId != expectedTarget\.EntityId\).*?var tableTarget = objectTable\.SearchByEntityId\(target\.EntityId\) as IPlayerCharacter; var exactCanonicalIdentity = tableTarget is not null && tableTarget\.Address == target\.Address && tableTarget\.GameObjectId == target\.GameObjectId && tableTarget\.EntityId == target\.EntityId;.*?SeitonReadinessProbe\.HasRangeAndLineOfSight\( localPlayer, target, actionId, out _\).*?NinjaSeitonProtectionProbe\.TryFindExecuteBlockingStatus\( target, out var executeBlockingStatusId, out _\).*?executeBlockingStatusId, validActionTarget, rangeAndLineOfSight') {
    throw 'Every NIN Seiton candidate must re-resolve one canonical e-slot, match both exact actor IDs/address, and pass FFXIV native range/LoS.'
}
$ninjaConsume = [regex]::Match($ninjaSeiton, 'inputClaimed\s*=\s*true\s*;\s*\r?\n\s*inputFrame\.Consume\(\);')
$ninjaFrozenResolve = [regex]::Match($ninjaSeiton, 'ResolveFrozenIntent\(\s*localPlayer!,\s*retry\.Intent,\s*finalResolvedActionId\)')
$ninjaIntentRevalidation = [regex]::Match($ninjaSeiton, 'NinjaSeitonDispatchRules\.CanUseExactIntent\s*\(')
$ninjaTryUse = [regex]::Match($ninjaSeiton, 'TryUseSeitonOnce\s*\(\s*localPlayer!,\s*intent,\s*out attempted,')
$ninjaNativeCall = [regex]::Match($ninjaSeiton, 'actionManager->UseAction\s*\(')
if (-not $ninjaConsume.Success -or -not $ninjaFrozenResolve.Success -or
    -not $ninjaIntentRevalidation.Success -or -not $ninjaTryUse.Success -or -not $ninjaNativeCall.Success -or
    $ninjaFrozenResolve.Index -gt $ninjaIntentRevalidation.Index -or
    $ninjaConsume.Index -gt $ninjaTryUse.Index -or
    $ninjaTryUse.Index -gt $ninjaNativeCall.Index) {
    throw 'NIN Seiton must revalidate only its frozen target, then claim the current scheduler frame before its bounded native request.'
}
$ninjaPostConsumeWindow = $ninjaSeiton.Substring(
    $ninjaConsume.Index,
    $ninjaTryUse.Index + $ninjaTryUse.Length - $ninjaConsume.Index)
if ($ninjaPostConsumeWindow -match '\b(ResolveExactCandidates|SelectBestCandidateIndex)\s*\(' -or
    $ninjaPostConsumeWindow -match '\bexecuteTracker\.Enemies\b') {
    throw 'After scheduler-frame claim NIN Seiton may use only the frozen intent; it must never rerank or choose an alternate.'
}
if ($normalizedNinjaSeiton -notmatch 'BuildExactSlotCandidate\( localPlayer, actionId, intent\.EnemySlot, intent\.Target\)' -or
    $normalizedNinjaSeiton -notmatch 'actionManager->UseAction\( ActionType\.Action, intent\.ActionId, intent\.Target\.GameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)') {
    throw 'NIN Seiton final validation and UseAction must retain the one frozen slot, exact actor, and exact adjusted action with no fallback.'
}
$ninjaTryUseMethod = [regex]::Match(
    $normalizedNinjaSeiton,
    'private unsafe ClientActionAttemptOutcome TryUseSeitonOnce\(.*?\) \{(?<Body>.*?)\} private BoundaryThresholdResult ReadFrozenThresholdAtUseActionBoundary')
$ninjaBoundaryThresholdMethod = [regex]::Match(
    $normalizedNinjaSeiton,
    'private BoundaryThresholdResult ReadFrozenThresholdAtUseActionBoundary\(.*?\) \{(?<Body>.*?)\} private static bool IsValidAtOrAboveHalf')
$ninjaTryUseBody = $ninjaTryUseMethod.Groups['Body'].Value
$ninjaBoundaryThresholdBody = $ninjaBoundaryThresholdMethod.Groups['Body'].Value
if (-not $ninjaTryUseMethod.Success -or
    -not $ninjaBoundaryThresholdMethod.Success -or
    $ninjaTryUseBody -notmatch 'nearAssist\.RunWithoutRedirect\(\(\) =>.*?SeitonReadinessProbe\.TryGetReadyAction\( localPlayer, out var resolvedActionId\).*?ResolveFrozenIntent\( localPlayer, intent, resolvedActionId\).*?NinjaSeitonDispatchRules\.CanUseExactIntent\( intent, frozenCandidate, currentLocalIdentity, resolvedActionId, actionLocallyReady: true\).*?ReadFrozenThresholdAtUseActionBoundary\( intent, out var currentHp, out var maximumHp, out var executeBlockingStatusId\).*?ExecuteBlockingStatusId = executeBlockingStatusId.*?if \(thresholdResult == BoundaryThresholdResult\.Protected\).*?protectionDriftAtBoundary = true; return false;.*?if \(thresholdResult != BoundaryThresholdResult\.BelowHalf\).*?return false;.*?thresholdRevalidatedAtBoundary = true; boundaryBefore = ClientActionAttemptBoundary\.Capture\( actionManager, intent\.ActionId\); attemptedAtBoundary = true; var clientAccepted = actionManager->UseAction\( ActionType\.Action, intent\.ActionId, intent\.Target\.GameObjectId, 0, ActionManager\.UseActionMode\.None, 0\); boundaryAfter = ClientActionAttemptBoundary\.Capture\( actionManager, intent\.ActionId\); return clientAccepted;.*?return attemptedAtBoundary \? ClientActionAttemptBoundaryRules\.Classify\( accepted, intent\.ActionId, boundaryBefore, boundaryAfter\) : softUnavailableAtBoundary \? ClientActionAttemptOutcome\.SoftUnavailable : ClientActionAttemptOutcome\.NotInvoked;' -or
    $ninjaTryUseBody -match '\b(ResolveExactCandidates|SelectBestCandidateIndex)\s*\(|\bexecuteTracker\.Enemies\b') {
    throw 'At the NIN UseAction boundary, the internal bypass must revalidate only the same frozen action/S-slot/GOID/EID, perform the latest strict sub-50 HP read, and classify the complete pre/post native fingerprint with no rerank or alternate.'
}
if ($ninjaBoundaryThresholdBody -notmatch 'EnemySlotResolver\.Resolve\(objectTable, intent\.EnemySlot\).*?target!\.GameObjectId != intent\.Target\.GameObjectId.*?target\.EntityId != intent\.Target\.EntityId.*?objectTable\.SearchByEntityId\(target\.EntityId\) as IPlayerCharacter.*?tableTarget\.Address != target\.Address.*?tableTarget\.GameObjectId != target\.GameObjectId.*?tableTarget\.EntityId != target\.EntityId.*?currentHp = target\.CurrentHp; maximumHp = target\.MaxHp;.*?target\.IsDead.*?!target\.IsTargetable.*?!ExecuteThreshold\.HasValidHp\(currentHp, maximumHp\).*?NinjaSeitonProtectionProbe\.TryFindExecuteBlockingStatus\( target, out executeBlockingStatusId, out _\).*?BoundaryThresholdResult\.Protected.*?ExecuteThreshold\.IsBelowHalf\(currentHp, maximumHp\).*?BoundaryThresholdResult\.BelowHalf.*?BoundaryThresholdResult\.AtOrAboveHalf' -or
    [regex]::Matches($ninjaBoundaryThresholdBody, '\btarget\.CurrentHp\b').Count -ne 1 -or
    [regex]::Matches($ninjaBoundaryThresholdBody, '\btarget\.MaxHp\b').Count -ne 1) {
    throw 'The final NIN threshold read must resolve only the frozen S-slot and exact GOID/EID/address, read that actor HP once, reject invalid/dead/untargetable state, and treat exactly 50 percent or higher as terminal cancellation.'
}
$ninjaSeitonProtectionProbe = Read-RequiredSource $ninjaSeitonProtectionProbePath 'NIN Seiton protection probe'
Assert-Literals $ninjaSeitonProtectionProbe @(
    'foreach (var status in player.StatusList)',
    'NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(',
    'statusId = status.StatusId',
    'remainingTime = status.RemainingTime'
) 'Exact live NIN Seiton protection status scan'
if ($ninjaSeitonProtectionProbe -match '(?i)status\.Name|Name\.TextValue' -or
    [regex]::Matches($ninjaSeiton, 'NinjaSeitonProtectionProbe\.TryFindExecuteBlockingStatus\s*\(').Count -ne 2) {
    throw 'NIN Seiton protection must use only exact numeric status metadata at candidate, cast-cancel, and final native boundaries.'
}
$ninjaSeitonExecuteTracker = Read-RequiredSource (Join-Path $pluginServicesRoot 'ExecuteTracker.cs') 'NIN Seiton cue tracker'
$normalizedNinjaSeitonExecuteTracker = $ninjaSeitonExecuteTracker -replace '\s+', ' '
if ($normalizedNinjaSeitonExecuteTracker -notmatch 'var executeProtected = NinjaSeitonProtectionProbe\.TryFindExecuteBlockingStatus\( player, out _, out _\); var seitonTargetReady = seitonResourceReady && !executeProtected;.*?PersistentSeitonCueRules\.Observe\( state\.SeitonCue, seitonTargetReady,.*?hardReset: !isNinja \|\| !metadata\.SeitonVerified \|\| executeProtected\)') {
    throw 'Persistent Seiton execute/preparation cues must clear while the exact enemy has execute-blocking protection.'
}
$ninjaCastCancellationMethod = [regex]::Match(
    $normalizedNinjaSeiton,
    'private HeldCastCancellationRequest\? CreateCastCancellationRequest\(.*?\) \{(?<Body>.*?)\} private ulong NextIntentEpochToken')
if (-not $ninjaCastCancellationMethod.Success -or
    $ninjaCastCancellationMethod.Groups['Body'].Value -notmatch 'ResolveFrozenIntent\( localPlayer, frozen\.Intent, resolvedActionId\).*?candidate\.HasExecuteBlockingProtection.*?executeBlockingStatusId = candidate\.ExecuteBlockingStatusId; return null;.*?NinjaSeitonDispatchRules\.CanUseExactIntent\( frozen\.Intent, candidate, currentLocalIdentity, resolvedActionId, actionLocallyReady: true\).*?new HeldCastCancellationRequest\(') {
    throw 'NIN Seiton may request cast cancellation only after exact frozen actor and execute-protection revalidation.'
}
$ninjaSeitonSelfTests = Read-RequiredSource $ninjaSeitonDispatchSelfTestsPath 'NIN Seiton dispatch self-tests'
Assert-Literals $ninjaSeitonSelfTests @(
    'public static void ExecuteBlockingProtectionStatusSetIsExact()',
    'public static void ProtectedTargetsAreSkippedAndFrozenProtectionDriftCancels()',
    'public static void HeldLevelUsesOneAcceptedAdjustedActionEpochAtATime()',
    'NinjaSeitonDispatchRules.BeginAcceptedHold(',
    'NinjaSeitonDispatchRules.CanOpenAdjustedActionEpoch(',
    '"same accepted base epoch cannot repeat"',
    '"adjusted follow-up is one distinct epoch"',
    '"spent follow-up epoch cannot reopen after terminal drift"',
    '"accepted follow-up cannot repeat"',
    '"key release ends accepted ownership"',
    'alternate with { CurrentHp = 50, MaximumHp = 100 }',
    '"healing to exactly half cancels the frozen intent"',
    'alternate with { CurrentHp = 51, MaximumHp = 100 }',
    '"healing above half cancels the frozen intent"'
) 'NIN exact-50 and above-50 frozen-target cancellation tests'
Assert-Literals $coreSelfTestProgramForGuardian @(
    'NinjaSeitonDispatchSelfTests.ExecuteBlockingProtectionStatusSetIsExact',
    'NinjaSeitonDispatchSelfTests.ProtectedTargetsAreSkippedAndFrozenProtectionDriftCancels',
    'NinjaSeitonDispatchSelfTests.HeldLevelUsesOneAcceptedAdjustedActionEpochAtATime'
) 'NIN held base/follow-up accepted-epoch test registration'
if ($ninjaSeiton -match '\b(IGameInteropProvider|Hook<|HookFromAddress|SignatureAttribute|SigScanner|ITargetManager|TargetManager|SetTarget|ResolvePlaceholder)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=' -or
    $ninjaSeiton -cmatch '\b(RetryAction|RetryDispatch|QueuedAction|QueueAction|PendingDispatch|BufferedDispatch)\b' -or
    $ninjaSeiton -match '(?:->|\.)Original\s*\(') {
    throw 'NIN Seiton must use only the existing internal redirect bypass and shared bounded retry policy; it must never hook, custom-queue, mutate, or depend on a visible target.'
}
Assert-Literals $pluginSource @(
    'personalStatus.NinjaSeitonDiagnostics',
    '[Seiton Sense] ninja-seiton[decision={ninja.Decision},reason={ninja.Reason}',
    'ready={ninja.LocallyReady},action={ninja.ResolvedActionId}',
    'candidates={ninja.CandidateCount},S={ninja.EnemySlot}',
    'hp={ninja.RevalidatedCurrentHp}/{ninja.RevalidatedMaximumHp}',
    'protection={ninja.ExecuteBlockingStatusId}',
    'boundary<50={ninja.BoundaryThresholdRevalidated}',
    'threshold-cancel={ninja.ThresholdDriftCancelled}/',
    '{ninja.ThresholdDriftCancellationCount}',
    'protection-cancel={ninja.ProtectionDriftCancelled}/',
    '{ninja.ProtectionDriftCancellationCount}',
    'fresh={ninja.FreshGameplayKey},claimed={ninja.InputClaimed}',
    'attempt={ninja.UseActionAttempted}/{ninja.UseActionAccepted}',
    'count={ninja.AttemptCount}/{ninja.AcceptedCount}',
    'resolve={ninja.CandidateResolution},last={ninja.LastEvent}'
) 'Truthful NIN Seiton source diagnostics'
$ninjaDebugStart = $pluginSource.IndexOf('[Seiton Sense] ninja-seiton[')
$ninjaDebugEnd = if ($ninjaDebugStart -ge 0) {
    $pluginSource.IndexOf('[Seiton Sense] monk-reply[', $ninjaDebugStart)
} else {
    -1
}
if ($ninjaDebugStart -lt 0 -or $ninjaDebugEnd -le $ninjaDebugStart -or
    $pluginSource.Substring($ninjaDebugStart, $ninjaDebugEnd - $ninjaDebugStart) -match '(?i)\b(landed|killed|executed successfully|server accepted)\b') {
    throw 'NIN Seiton diagnostics may report only attempted/client-accepted telemetry, never a landed action or kill.'
}

# Monk Earth's Reply is a separate default-off direct self-action boundary. It
# may dispatch only exact adjusted 29483, after self-Purify declines priority,
# and must spend the continuous resonance before its sole native attempt.
$monkEarthReplyRules = Read-RequiredSource (Join-Path $coreRoot 'MonkEarthReplyRules.cs') 'Monk Earth Reply rules'
$monkEarthReply = Read-RequiredSource $monkEarthReplyProbePath 'Monk Earth Reply probe'
$normalizedMonkEarthReplyRules = $monkEarthReplyRules -replace '\s+', ' '
$normalizedMonkEarthReply = $monkEarthReply -replace '\s+', ' '
Assert-Literals $monkEarthReplyRules @(
    'MonkJobId = 20',
    'RiddleOfEarthActionId = 29_482',
    'EarthsReplyActionId = 29_483',
    'EarthResonanceStatusId = 3_171',
    'EarthsReplyProcStatusRowId = 94',
    'ResonanceMissingGraceMilliseconds = 150',
    'SpentUntilResonanceGone',
    'observation.AdjustedActionId != EarthsReplyActionId',
    'observation.HigherPriorityClaimed',
    'Phase = MonkEarthReplyPhase.SpentUntilResonanceGone',
    '(ulong)currentHp * 100UL <= (ulong)maximumHp * (uint)thresholdPercent',
    'remainingSeconds <= thresholdSeconds'
) 'Exact one-resonance Monk Earth Reply policy'
if ($normalizedMonkEarthReplyRules -notmatch 'if \(observation\.HigherPriorityClaimed\) return Waiting\(current, MonkEarthReplyDecisionReason\.HigherPriorityClaimed\); var spent = current with \{ Phase = MonkEarthReplyPhase\.SpentUntilResonanceGone' -or
    $normalizedMonkEarthReplyRules -notmatch 'var trigger = lowHpTriggered \? MonkEarthReplyTrigger\.LowHp : expiryTriggered \? MonkEarthReplyTrigger\.Expiry : MonkEarthReplyTrigger\.None') {
    throw 'Monk Earth Reply must check low-HP then expiry, yield to higher-priority Purify, and spend before dispatch.'
}
if ($monkEarthReplyRules -match '\b(UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|RetryAction|RetryDispatch)\b') {
    throw 'Pure Monk Earth Reply rules must never call actions, mutate targets, or retry.'
}

if ([regex]::Matches($monkEarthReply, '(?:->|\.)UseAction\s*\(').Count -ne 1) {
    throw 'Monk Earth Reply probe must contain exactly one native UseAction call.'
}
Assert-Literals $monkEarthReply @(
    'TryGetExactEarthResonance(localPlayer!, out remainingSeconds)',
    'actionManager->GetAdjustedActionId(MonkEarthReplyRules.RiddleOfEarthActionId)',
    'MonkEarthReplyRules.EarthsReplyActionId',
    'MonkEarthReplyRules.EarthResonanceStatusId',
    'matches > 1',
    'localPlayer.ClassJob.RowId == MonkEarthReplyRules.MonkJobId',
    'native->EntityId == localPlayer.EntityId',
    'state = decision.NextState',
    'TryUseEarthsReplyOnce(localPlayer!, out attempted)',
    'nearAssist.RunWithoutRedirect',
    'ActionType.Action',
    'ActionManager.UseActionMode.None',
    'will not be retried for this Earth Resonance'
) 'Exact local Monk Earth Reply runtime'
if ($monkEarthReply -match '\bstatus\.Address\b') {
    throw 'Monk Earth Resonance must not depend on a fragile managed status-slot address.'
}
if ($normalizedMonkEarthReply -notmatch 'actionManager->UseAction\s*\(\s*ActionType\.Action\s*,\s*MonkEarthReplyRules\.EarthsReplyActionId\s*,\s*localPlayer\.GameObjectId\s*,\s*0\s*,\s*ActionManager\.UseActionMode\.None\s*,\s*0\s*\)' -or
    $normalizedMonkEarthReply -match 'UseAction\s*\([^)]*RiddleOfEarthActionId') {
    throw 'Monk helper must issue only ActionType.Action 29483 to the exact local player and must never fall back to 29482.'
}
$monkCommit = [regex]::Match($monkEarthReply, 'state\s*=\s*decision\.NextState\s*;')
$monkTryUse = [regex]::Match($monkEarthReply, '\bTryUseEarthsReplyOnce\s*\(\s*localPlayer!')
$monkNativeCall = [regex]::Match($monkEarthReply, 'actionManager->UseAction\s*\(')
if (-not $monkCommit.Success -or -not $monkTryUse.Success -or -not $monkNativeCall.Success -or
    $monkCommit.Index -gt $monkTryUse.Index -or $monkTryUse.Index -gt $monkNativeCall.Index) {
    throw 'Monk Earth Reply must store its spent decision before its one native action attempt.'
}
if ($monkEarthReply -match '(?-i:\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction)\b)' -or
    $monkEarthReply -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Monk Earth Reply must never retry, custom-queue, or mutate a visible target.'
}

Assert-Literals $metadataGuard @(
    'MonkEarthReplyVerified',
    'ValidateFeature("Monk Earth''s Reply"',
    'MonkEarthReplyRules.RiddleOfEarthActionId',
    'MonkEarthReplyRules.EarthsReplyActionId',
    'MonkEarthReplyRules.EarthResonanceStatusId',
    'MonkEarthReplyRules.EarthsReplyProcStatusRowId',
    'baseAction.ClassJob.RowId == MonkEarthReplyRules.MonkJobId',
    'followUp.ClassJob.RowId == MonkEarthReplyRules.MonkJobId',
    'followUp.ActionProcStatus.RowId == MonkEarthReplyRules.EarthsReplyProcStatusRowId',
    'procStatus.Status.RowId == MonkEarthReplyRules.EarthResonanceStatusId',
    'Can only be executed while under the effect of Earth Resonance.',
    'This action cannot be assigned to a hotbar.'
) 'Independent Monk Earth Reply metadata gate'

$monkObserve = [regex]::Match($personalStatus, '\bmonkEarthReply\.Observe\s*\(')
if (-not $monkObserve.Success -or $monkObserve.Index -lt $smartKardiaObserve.Index -or
    $normalizedPersonalStatus -notmatch 'var isSupportedPvPContext = context != SupportedPvPContext\.None' -or
    $normalizedPersonalStatus -notmatch 'var monk = monkEarthReply\.Observe\( localPlayer, isSupportedPvPContext, configuration\.Enabled && configuration\.EnableMonkEarthReplyHelper && isMonk && !guardActive, metadata\.MonkEarthReplyVerified, configuration\.MonkEarthReplyOnLowHp, configuration\.MonkEarthReplyBeforeExpiry, configuration\.MonkEarthReplyHpPercent, configuration\.MonkEarthReplyExpirySeconds, purifyClaimedPriority \|\| jobSpecificHeldClaimedPriority \|\| smartRecuperateClaimedPriority \|\| defensiveUtilityClaimedPriority \|\| pressureEscapeClaimedPriority \|\| kardia\.UseActionAttempted \|\| emergencyInputFrame\.IsConsumed') {
    throw 'Event Monk Earth Reply must remain last after event Kardia, require exact Monk plus no Guard, and yield whenever any earlier helper claimed or attempted.'
}

# DRK Plunge closes the job-specific physical-hold tier before generic helpers. The first
# request owns one ordinary held-key generation; continued hold can open exactly
# one later request only after a known not-ready -> ready cooldown transition.
$darkKnightPlungeRules = Read-RequiredSource $darkKnightPlungeRulesPath 'DRK Plunge rules'
$normalizedDarkKnightPlungeRules = $darkKnightPlungeRules -replace '\s+', ' '
$darkKnightPlunge = Read-RequiredSource $darkKnightPlungeProbePath 'DRK Plunge runtime'
$normalizedDarkKnightPlunge = $darkKnightPlunge -replace '\s+', ' '
$darkKnightPlungeSelfTests = Read-RequiredSource $darkKnightPlungeSelfTestsPath 'DRK Plunge self-tests'
Assert-Literals $darkKnightPlungeRules @(
    'public const uint DarkKnightJobId = 32;',
    'public const uint DarkKnightClassJobCategoryId = 98;',
    'public const uint ActionId = 29_092;',
    'public const uint IconId = 9_150;',
    'public const uint MaximumHpPercent = 30;',
    'public const float MaximumCenterDistanceYalms = 10f;',
    'public const int ExpectedRuntimeRecastGroupIndex = 1;',
    'public const int ExpectedAdjustedRecastMilliseconds = 12_000;',
    'CurrentReadyEpochToken: 1,',
    'SpentReadyEpochToken: 1);',
    'DarkKnightPlungeHoldOutcome.PreservedUnknown',
    'DarkKnightPlungeHoldOutcome.WaitingForReady',
    'DarkKnightPlungeHoldOutcome.OpenedReadyEpoch',
    'public static bool TrySpendReadyEpoch(',
    '!candidate.TargetGuardActive',
    'candidate.HasNativeRangeAndLineOfSight',
    'localJobId == DarkKnightJobId',
    '!actionHelpersSuppressedByGuard',
    '!higherPriorityClaimed',
    'exactHeldKeyStillDown',
    'cooldownStateKnown',
    'cooldownReady',
    'actionStructurallyReady'
) 'Exact job-tier DRK Plunge Core policy'
if ($normalizedDarkKnightPlungeRules -notmatch '\(ulong\)currentHp \* 100UL <= \(ulong\)maximumHp \* MaximumHpPercent' -or
    $normalizedDarkKnightPlungeRules -notmatch 'centerDistanceSquared <= MaximumCenterDistanceSquared' -or
    $normalizedDarkKnightPlungeRules -notmatch 'if \(!observation\.CooldownStateKnown\).*?PreservedUnknown.*?if \(!observation\.CooldownReady\).*?ObservedCooldownUnavailable = true.*?if \(!state\.ObservedCooldownUnavailable\).*?ReadyEpochUnchanged.*?CurrentReadyEpochToken = NextToken' -or
    $normalizedDarkKnightPlungeRules -notmatch 'var health = \(\(ulong\)left\.CurrentHp \* right\.MaximumHp\)\.CompareTo\( \(ulong\)right\.CurrentHp \* left\.MaximumHp\); if \(health != 0\) return health; var slot = left\.EnemySlot\.CompareTo\(right\.EnemySlot\);.*?left\.Actor\.EntityId.*?left\.Actor\.GameObjectId' -or
    $darkKnightPlungeRules -match '\b(?:UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b|\.(?:Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)') {
    throw 'DRK Plunge Core must keep inclusive 30-percent/10-yalm eligibility, HP-ratio/slot/identity ranking, observed cooldown epochs, and no runtime or target dependencies.'
}
$darkKnightPlungeTestMethods = @(
    'ExactIdentityThresholdAndRangeArePinned',
    'CandidateRankingAndAmbiguityAreDeterministic',
    'ContinuousHoldRequiresAProvenCooldownEpoch',
    'InitialAndRepeatDispatchUseDistinctOwnership',
    'FrozenIntentRequiresEveryTerminalGate'
)
foreach ($method in $darkKnightPlungeTestMethods) {
    Assert-Literals $darkKnightPlungeSelfTests @("public static void $method()") "DRK Plunge self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("DarkKnightPlungeSelfTests.$method") "DRK Plunge test registration $method"
}
if ([regex]::Matches($darkKnightPlungeSelfTests, '\bpublic static void\s+\w+\s*\(').Count -ne 5 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bDarkKnightPlungeSelfTests\.\w+').Count -ne 5) {
    throw 'All five DRK Plunge threshold, ranking, cooldown-epoch, ownership, and terminal-gate tests must remain registered exactly once.'
}
Assert-Literals $metadataGuard @(
    'DarkKnightPlungeVerified',
    'ValidateFeature("Dark Knight Plunge"',
    'return guardVerified &&',
    'string.Equals(action.Name.ToString(), "Plunge", StringComparison.Ordinal)',
    'action.Icon == DarkKnightPlungeRules.IconId',
    'action.ClassJob.RowId == DarkKnightPlungeRules.DarkKnightJobId',
    'DarkKnightPlungeRules.DarkKnightClassJobCategoryId',
    'action.Range == 20',
    'action.Recast100ms == 120',
    'action.CooldownGroup == 2',
    'action.CanTargetHostile',
    'action.RequiresLineOfSight',
    'action.AffectsPosition',
    'Cannot be executed while bound.'
) 'Exact DRK Plunge installed-sheet metadata gate'
Assert-Literals $darkKnightPlunge @(
    'DarkKnightPlungeRules.ObserveOwnedHold(',
    'inputFrame.IsGameplayKeyPhysicallyDown(ownedKey)',
    'inputFrame.Consume()',
    'DarkKnightPlungeRules.TrySpendReadyEpoch(',
    'DarkKnightPlungeRules.BeginOwnedHold(',
    'frozen.Intent.HeldKeyCode',
    'EnemySlotResolver.Resolve(objectTable, slot)',
    'objectTable.SearchByEntityId(player!.EntityId) as IPlayerCharacter',
    'actionManager->GetActionStatus(',
    'ActionManager.GetActionInRangeOrLoS(',
    'checkCastingActive: !anyLocalCastSignal',
    'checkCastingActive: true',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight(',
    'Vector3.DistanceSquared(',
    'DefensiveUtilityProbe.HasActiveGuard(target)',
    'EnemyCombatConstants.PvPBindStatusId',
    'nearAssist.TryGetRecentExactLocalGuardAttempt(',
    'DarkKnightPlungeRules.CanUseExactIntent(',
    'ResolveCurrentContext() != SupportedPvPContext.CrystallineConflict',
    'actionManager->GetRecastGroup(',
    'DarkKnightPlungeRules.ExpectedRuntimeRecastGroupIndex',
    'DarkKnightPlungeRules.ExpectedAdjustedRecastMilliseconds',
    'actionManager->CheckActionResources(',
    'HeldActionRetryRules.IsNativeBoundaryNearQueueable(',
    'HeldActionRetryRules.RetainsSchedulerFrame(',
    'HeldActionRetryRules.CanAttemptFrozenIntent(',
    'ClientActionAttemptBoundary.Capture(',
    'ClientActionAttemptBoundaryRules.Classify(',
    'HeldActionRetryRules.Complete(',
    'HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(',
    'HeldActionRetryRules.MaximumNativeAttempts',
    'nearAssist.RunWithoutRedirect(',
    'ActionManager.UseActionMode.None'
) 'Exact DRK Plunge native/runtime boundary with shared bounded retry'
if ([regex]::Matches($darkKnightPlunge, '(?:->|\.)UseAction\s*\(').Count -ne 1 -or
    $normalizedDarkKnightPlunge -notmatch 'actionManager->UseAction\( ActionType\.Action, intent\.ActionId, intent\.Target\.GameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)' -or
    $normalizedDarkKnightPlunge -notmatch 'actionManager->GetActionStatus\( ActionType\.Action, actionId, expectedTarget\.GameObjectId, checkRecastActive: true, checkCastingActive\) == 0' -or
    $normalizedDarkKnightPlunge -notmatch 'HasActionSpecificReadiness\(.*?PvPBindStatusId.*?IsActionOffCooldown.*?CheckActionResources' -or
    $normalizedDarkKnightPlunge -notmatch 'IsGlobalNativeBoundaryReady\(.*?HeldActionRetryRules\.IsNativeBoundaryNearQueueable\( actionManager->AnimationLock, localPlayer\.IsCasting, actionManager->CastActionId, actionManager->ActionQueued\)' -or
    $darkKnightPlunge -match '(?-i:\b(?:RetryAction|RetryDispatch|QueuedAction|QueueAction|PendingDispatch|BufferedDispatch|ITargetManager|TargetManager|SetTarget|UseActionLocation|ExecuteAction|SendAction)\b)|\.(?:Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)') {
    throw 'DRK Plunge must make only exact direct-GOID requests after action/range/LoS/Bind/resource and shared global-boundary checks, with no target mutation, alternate, or custom queue.'
}
$plungeInitialConsume = [regex]::Match($darkKnightPlunge, '\binputFrame\.Consume\s*\(')
$plungeRepeatSpend = [regex]::Match($darkKnightPlunge, '\bDarkKnightPlungeRules\.TrySpendReadyEpoch\s*\(')
$plungeCompleteAttempt = [regex]::Match($darkKnightPlunge, '\bCompleteAttempt\s*\(')
$plungeNativeCall = [regex]::Match($darkKnightPlunge, 'actionManager->UseAction\s*\(')
if (-not $plungeInitialConsume.Success -or -not $plungeRepeatSpend.Success -or
    -not $plungeCompleteAttempt.Success -or -not $plungeNativeCall.Success -or
    $plungeInitialConsume.Index -gt $plungeNativeCall.Index -or
    $plungeCompleteAttempt.Index -gt $plungeNativeCall.Index -or
    $plungeRepeatSpend.Index -lt $plungeNativeCall.Index -or
    $normalizedDarkKnightPlunge -notmatch 'if \(outcome == ClientActionAttemptOutcome\.ClientAccepted\).*?if \(frozen\.Intent\.IsRepeat\).*?DarkKnightPlungeRules\.TrySpendReadyEpoch\(.*?else.*?holdState = DarkKnightPlungeRules\.BeginOwnedHold') {
    throw 'DRK Plunge must claim the current frame before its native boundary, retain clean-false retries, and spend a repeat cooldown epoch or begin hold ownership only after client acceptance.'
}

$targetPressureTracker = Read-RequiredSource (Join-Path $pluginServicesRoot 'TargetPressureTracker.cs') 'Target pressure tracker'
$normalizedTargetPressureTracker = $targetPressureTracker -replace '\s+', ' '
if ($normalizedTargetPressureTracker -notmatch 'var pressureFeaturesEnabled = configuration\.ShowPressureCounter \|\| configuration\.ShowIncomingPressureOnNameplates \|\| configuration\.ShowTeamPressureOnNameplates \|\| configuration\.EnableNearAssistMacro \|\| configuration\.NearAssistPreferTeamPressure') {
    throw 'The shared Smart Target/Near Assist master must keep team-pressure production active independently of visible pressure surfaces and the Near Assist pressure preference.'
}
if ($normalizedTargetPressureTracker -notmatch 'supportedContext == SupportedPvPContext\.CrystallineConflict && \(\(isAllyRescueJob && configuration\.ExperimentalAllyRescueOnNextKey && metadata\.AllyRescueStatusesVerified\) \|\| oneShotAllyPressureRequested \|\| \(isBard && configuration\.EnableBardWardensPaeanPressureRedirect\) \|\| \(isPaladin && configuration\.PaladinGuardianLowAlly\) \|\| \(configuration\.EnableNearAssistMacro && configuration\.NearHelpPreferIncomingPressure\)\)') {
    throw 'Incoming ally pressure must remain CC-only with exact job gates for Ally Rescue, Smart Paean, and PLD Guardian, plus accepted-Eukrasia one-shot or explicit Near Help pressure.'
}
if ($normalizedTargetPressureTracker -notmatch 'var isAllyRescueJob = localJobId is EnemyCombatConstants\.WhiteMageJobId or EnemyCombatConstants\.BardJobId; var isReactiveCounterCcJob = isAllyRescueJob \|\| localJobId == EnemyCombatConstants\.NinjaJobId;' -or
    $normalizedTargetPressureTracker -notmatch 'configuration\.EnableDefensiveUtilities \|\| \(isReactiveCounterCcJob && configuration\.EnableReactiveCcUtilities && \(configuration\.ReactiveCcAfterEnemyPurify \|\| configuration\.ReactiveCcAfterEnemyGuard\)\) \|\| \(isScholar && configuration\.EnableScholarCriticalStrategyOnHeldKey\) \|\| configuration\.EnableAutoEnemyFocusMark') {
    throw 'Pressure tracking must remain independently active with exact WHM/BRD/NIN job gates for either post-Purify or post-Guard counter-CC and SCH ranking, plus defensive and Attack-1 consumers.'
}
if ($normalizedTargetPressureTracker -notmatch 'configuration\.EnableAutoEnemyFocusMark \|\| configuration\.ShowHighPressureWarning \|\| configuration\.PlayHighPressureWarningSound \|\| configuration\.EnablePressureEscapeSprintOnHeldKey;') {
    throw 'Direct pressure tracking must activate independently for each high-pressure visual, FFXIV-system-sound, or held-Sprint option.'
}
if ($targetPressureTracker -match '\bEnableSageKardia(?:OnHeldKey|AfterEukrasia)\b' -or
    $normalizedTargetPressureTracker -notmatch 'oneShotAllyPressureRequested = requestedAllyPressureAt >= 0 && now >= requestedAllyPressureAt && now - requestedAllyPressureAt < SmartKardiaRules\.TriggerLifetimeMilliseconds' -or
    $targetPressureTracker -match '\bShowCombatFrames\b') {
    throw 'Smart Kardia may request only a bounded fresh pressure publication, and the retired Combat Frames master must not keep pressure or protection scanning alive.'
}

# Isolation is a warning-only exact-CC reader. It resolves one native five-member
# party and samples FFXIV's 20y range/LoS result without acquiring an action path.
$isolationWarningRules = Read-RequiredSource $isolationWarningRulesPath 'Isolation warning rules'
$normalizedIsolationWarningRules = $isolationWarningRules -replace '\s+', ' '
$isolationAwareness = Read-RequiredSource $isolationAwarenessPath 'Isolation awareness service'
$normalizedIsolationAwareness = $isolationAwareness -replace '\s+', ' '
Assert-Literals $isolationWarningRules @(
    'ExpectedNonSelfPartyMembers = 4',
    'EnterDelayMilliseconds = 500',
    'ClearDelayMilliseconds = 200',
    '!observation.HasCompleteExactParty',
    'allies.Count != ExpectedNonSelfPartyMembers',
    'IsolationAllyReachability.Unknown',
    'IsolationAllyReachability.Connected',
    'IsolationWarningSignal.Isolated'
) 'Fail-closed exact-party isolation warning rules'
if ($normalizedIsolationWarningRules -notmatch 'if \(!ally\.IsAlive\) \{ if \(ally\.Reachability != IsolationAllyReachability\.Unavailable\) return IsolationWarningSignal\.Unknown; continue; \}.*?if \(!ally\.IsTargetable \|\| ally\.Reachability is IsolationAllyReachability\.Unknown or IsolationAllyReachability\.Unavailable\).*?if \(connected\) return IsolationWarningSignal\.Connected; return hasUnknownLiveAlly \? IsolationWarningSignal\.Unknown : IsolationWarningSignal\.Isolated') {
    throw 'Isolation must stay silent for incomplete/unknown live allies and warn only when all four exact non-self party observations prove no connection.'
}
Assert-Literals $isolationAwareness @(
    'ProbeActionId = 29_484',
    'UpdateIntervalMilliseconds = 100',
    'includeWolvesDenTesting: false',
    'context == SupportedPvPContext.CrystallineConflict',
    'partyIds.Count != 5',
    'partyIds.Distinct().Count() != 5',
    'partyIds.Contains(local.EntityId)',
    'ActionManager.GetActionInRangeOrLoS(',
    'ReadyResult => IsolationAllyReachability.Connected',
    'NotFacingResult => IsolationAllyReachability.Connected',
    'LineOfSightFailureResult => IsolationAllyReachability.Disconnected',
    'RangeFailureResult => IsolationAllyReachability.Disconnected',
    'action.Range == 20',
    'action.CanTargetParty',
    'action.RequiresLineOfSight'
) 'Exact CC native 20y/LoS isolation reader'
if ($isolationAwareness -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|ExecuteCommandInner|RaptureShellModule|MarkingController|ITargetManager|SetTarget|SetRawValue|FireCallback)\b|(?-i:\bTargetManager\b)|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Isolation awareness must remain read-only: no action, shell, marker, target, input, or native UI writes.'
}

# Automatic Attack-1 is default-off and owns nothing until an empty marker is
# observed to change to the exact e-slot actor with a changed marker timestamp.
$autoEnemyFocusMarkRules = Read-RequiredSource $autoEnemyFocusMarkRulesPath 'Auto enemy focus mark rules'
$normalizedAutoEnemyFocusMarkRules = $autoEnemyFocusMarkRules -replace '\s+', ' '
$normalizedAutoEnemyFocusMark = $autoEnemyFocusMark -replace '\s+', ' '
Assert-Literals $autoEnemyFocusMarkRules @(
    'candidate.GuardUnavailable',
    '(candidate.LowHp || candidate.LowMp)',
    '(true, true) => 3',
    '(true, false) => 2',
    '(false, true) => 1',
    'right.TeamTargetCount.CompareTo(left.TeamTargetCount)',
    'left.EnemySlot.CompareTo(right.EnemySlot)',
    'observedMarkerTime != markerTimeBeforeCommand',
    'observedMarkerTime == ownedMarkerTime',
    'ShouldClearConfirmedOwnership'
) 'Guard-down HP/MP Attack-1 selection and exact ownership rules'
if ($normalizedAutoEnemyFocusMarkRules -notmatch 'private static int Priority\(AutoEnemyFocusMarkCandidate candidate\) => \(candidate\.LowHp, candidate\.LowMp\) switch \{ \(true, true\) => 3, \(true, false\) => 2, \(false, true\) => 1, _ => 0, \}' -or
    $normalizedAutoEnemyFocusMarkRules -notmatch 'var priority = Priority\(right\)\.CompareTo\(Priority\(left\)\);.*?var hp = CompareRatio\(.*?var mp = CompareTrustedMp\(left, right\);.*?var teamTargets = right\.TeamTargetCount\.CompareTo\(left\.TeamTargetCount\);.*?left\.EnemySlot\.CompareTo\(right\.EnemySlot\)') {
    throw 'Attack-1 ranking must be Both > HP-only > MP-only, then lowest HP ratio, lowest trusted MP ratio, highest team focus, stable e-slot.'
}
Assert-Literals $autoEnemyFocusMark @(
    'MinimumCommandIntervalMilliseconds = 1_000',
    'ConfirmationTimeoutMilliseconds = 1_500',
    'context == SupportedPvPContext.CrystallineConflict',
    'metadata.GuardVerified',
    'trackerDiagnostics.ResolvedSlots == 5',
    'trackerDiagnostics.SlotCapacity == 5',
    'marking->Markers[0]',
    'marking->MarkerTimes[0]',
    'if (observedMarker != 0)',
    'Attack-1 is occupied; no overwrite',
    'CanConfirmOwnership(',
    'CanClearOwnedMarker(',
    'ShouldClearConfirmedOwnership(',
    'TryClearOwnedOnDispose()',
    'blockedMarkCandidate == desiredIdentity',
    'TryGetTextInputState(out var textInputActive)',
    'TryResolveExactSlotIdentity(',
    'CanIssueCommand(now)',
    'TargetPressureRuntimeSnapshot? pressure',
    'pressure?.Find(enemy.GameObjectId, enemy.EntityId)',
    'pressureEnemy?.TeamTargetCount ?? 0'
) 'Exact empty-to-owned Attack-1 lifecycle'
if ($autoEnemyFocusMark -match '\bmetadata\.RecuperateVerified\b|\btrackerDiagnostics\.RecuperateMetadataVerified\b') {
    throw 'Attack-1 structural/clear gates may require Guard metadata but must not globally disable HP-only selection or owned cleanup when Recuperate metadata drifts.'
}
if ($normalizedAutoEnemyFocusMark -match 'if \(!pressure\.Active \|\| !pressure\.PressureActive\)' -or
    $normalizedAutoEnemyFocusMark -notmatch 'var exactPressure = pressure\.Active && pressure\.PressureActive \? pressure : null; var buildSucceeded = TryBuildCandidates\(exactPressure, out var candidates\)' -or
    $normalizedAutoEnemyFocusMark -notmatch 'var pressureEnemy = pressure\?\.Find\(enemy\.GameObjectId, enemy\.EntityId\);.*?pressureEnemy\?\.TeamTargetCount \?\? 0') {
    throw 'Attack-1 eligibility must not require pressure telemetry; only the highest known team-target count may act as an optional tie-break.'
}
if ($normalizedAutoEnemyFocusMark -notmatch 'var structuralExactContext = localIdentityValid && context == SupportedPvPContext\.CrystallineConflict && metadata\.GuardVerified;' -or
    $normalizedAutoEnemyFocusMark -notmatch 'if \(AutoEnemyFocusMarkRules\.ShouldClearConfirmedOwnership\( configuration\.Enabled, configuration\.EnableAutoEnemyFocusMark, ownership is not null\)\) \{ HandleOwnedClear\(' -or
    $normalizedAutoEnemyFocusMark -notmatch 'if \(ownership is \{ \} currentOwnership && \(observedMarker != currentOwnership\.GameObjectId \|\| observedMarkerTime != currentOwnership\.MarkerTime\)\) \{ Relinquish\(' -or
    $normalizedAutoEnemyFocusMark -notmatch 'if \(!TryResolveExactSlotIdentity\(owned\.EnemySlot, out var currentSlotIdentity\) \|\| !AutoEnemyFocusMarkRules\.CanClearOwnedMarker\(.*?\)\) \{ Relinquish\(') {
    throw 'Attack-1 must clear only confirmed unchanged ownership, including disable; any actor/slot/marker-time drift must relinquish without a command.'
}
if ($normalizedAutoEnemyFocusMark -notmatch 'public void Dispose\(\).*?TryClearOwnedOnDispose\(\); Relinquish\("Disposed"\)' -or
    $normalizedAutoEnemyFocusMark -notmatch 'TryClearOwnedOnDispose\(\).*?ownership is not \{ \} owned \|\| pending is not null.*?context != SupportedPvPContext\.CrystallineConflict \|\| !metadata\.GuardVerified \|\| !TryGetTextInputState\(out var textInputActive\) \|\| textInputActive.*?AutoEnemyFocusMarkRules\.CanClearOwnedMarker\(.*?commands\.TryClearAttack1\(owned\.EnemySlot, now\) != ReviewedPvpCommandDispatchResult\.Invoked') {
    throw 'Dispose may issue only one best-effort owned clear after exact CC, text, slot/entity, marker, timestamp, and rate-limit revalidation.'
}
if ($normalizedAutoEnemyFocusMark -notmatch 'var markResult = commands\.TryMarkAttack1\(desired\.Value\.EnemySlot, now\); if \(markResult == ReviewedPvpCommandDispatchResult\.MarkerRateLimited\).*?return;.*?blockedMarkCandidate = desiredIdentity; if \(markResult != ReviewedPvpCommandDispatchResult\.Invoked\).*?return;.*?lastCommandAt = now; markCommands\+\+; pending = new PendingMarkerCommand' -or
    $normalizedAutoEnemyFocusMark -notmatch 'private bool CanIssueCommand\(long now\) => now >= lastCommandAt && now - lastCommandAt >= MinimumCommandIntervalMilliseconds') {
    throw 'Attack-1 must issue through the shared marker reservation, consume non-rate-limit failures for the candidate transition, and retain its own at-least-one-second transition limit.'
}
Assert-Literals $pluginSource @(
    'new AutoEnemyFocusMarkService(',
    'new IsolationAwarenessService(',
    'autoEnemyFocusMark.Start()',
    'isolationAwareness.Start()',
    'isolationAwareness.Dispose()',
    'autoEnemyFocusMark.Dispose()',
    'personalStatus.DefensiveUtilityDiagnostics',
    'autoEnemyFocusMark.Diagnostics',
    'isolationAwareness.Diagnostics'
) 'Utility-awareness lifecycle and live diagnostics wiring'
$allyRescueBuffer = Read-RequiredSource (Join-Path $coreRoot 'AllyRescueBufferRules.cs') 'Ally Rescue status-bound held-lease rules'
Assert-Literals $allyRescueBuffer @(
    'StatusBoundBufferMilliseconds = -1',
    'DefaultBufferMilliseconds = StatusBoundBufferMilliseconds',
    'NativeRetryThrottleMilliseconds =',
    'HeldActionRetryRules.NativeRetryThrottleMilliseconds',
    'MaximumNativeAttempts = HeldActionRetryRules.MaximumNativeAttempts',
    'SpentIntents',
    'ResolveCandidateEntryTrigger',
    'AllowHeldKeyAtCandidateEntry',
    'CompleteNativeAttempt(',
    'HeldActionRetryRules.Complete(',
    'AllyRescueNativeAttemptOutcome.RetryScheduled',
    'AllyRescueNativeAttemptOutcome.SoftWait',
    'previous.SpentIntents.Add(intent)'
) 'Ally Rescue status-bound exact hold and shared clean-false retry rules'

$nearAssist = Read-RequiredSource $nearAssistPath 'Near Assist redirector'
$normalizedNearAssist = $nearAssist -replace '\s+', ' '
Assert-Literals $nearAssist @(
    'HookFromAddress<ActionManager.Delegates.UseAction>',
    'ActionManager.MemberFunctionPointers.UseAction',
    'TryGetRecentExactLocalGuardAttempt(',
    'ObserveExactLocalGuardActivationAttempt(thisPtr, actionType, actionId)',
    'ResolveActionId(actionManager, actionType, actionId) !=',
    'EnemyCombatConstants.GuardActionId',
    'new LocalGuardActionAttempt(',
    'clientState.TerritoryType',
    'local!.GameObjectId',
    'local.EntityId',
    'NearAssistOneShotRules.Arm',
    'NearAssistOneShotRules.ArmFallback',
    'NearAssistOneShotRules.Observe',
    'NearAssistSelectionRules.ClassifyPlayableJob',
    'NearAssistPressureSelectionRules.SelectBestIndex',
    'configuration.NearAssistPreferTeamPressure',
    'EnemySlotResolver.Resolve',
    'GetNativeHardTargetId',
    'GetActionInRangeOrLoS',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight',
    'SupportedPvPContext.CrystallineConflict',
    'SmartWardensPaeanInterceptResult.Vanilla(',
    'smartWardensPaean.Evaluate(',
    'IsLocalGuardActiveOrPropagating()',
    'smartPaeanResult.ShouldSuppress',
    'smartPaeanResult.ShouldRedirect',
    'smartPaeanResult.ForwardTargetId',
    'smartWardensPaean.RecordNativeResult(smartPaeanResult, clientAccepted)',
    'TokenLifetimeMilliseconds = 750',
    'NearAssistCarrierRules.IsFallbackCarrier',
    'IsEligibleRedirectAction',
    'CarrierEnemyEntityId',
    'CarrierEnemyGameObjectId',
    'mode != ActionManager.UseActionMode.Queue',
    'mode == ActionManager.UseActionMode.None',
    'oneShotState = NearAssistOneShotState.Initial',
    'oneShotState = decision.NextState',
    'token.HasRedirectCandidate',
    'InvalidCarrierTargetId = 0',
    'consumedFallbackCarrier ? InvalidCarrierTargetId : targetId',
    'if (!rewritten && consumedFallbackCarrier)',
    'forwardedTargetId = InvalidCarrierTargetId'
) 'Near Assist redirector'
$nearAssistSelection = Read-RequiredSource (Join-Path $coreRoot 'NearAssistSelectionRules.cs') 'Near Assist smart selection rules'
Assert-Literals $nearAssistSelection @(
    'RolePreferenceWindowYalms = 8f',
    'NearAssistAllyRole.RangedDamage',
    'NearAssistAllyRole.MeleeDamage',
    'NearAssistAllyRole.SupportOrUnknown',
    '23 or 25 or 27 or 31 or 35 or 38 or 42',
    '20 or 22 or 30 or 34 or 39 or 41'
) 'Near Assist smart selection rules'
if ([regex]::Matches($nearAssist, 'HookFromAddress<ActionManager\.Delegates\.UseAction>').Count -ne 1) {
    throw 'Near Assist must create exactly one generated ActionManager.UseAction hook.'
}
if ([regex]::Matches($nearAssist, 'HookFromAddress<ActionManager\.Delegates\.UseActionLocation>').Count -ne 1 -or
    [regex]::Matches($nearAssist, '\buseActionHook!\.Original\s*\(').Count -ne 1 -or
    [regex]::Matches($nearAssist, '\buseActionLocationHook!\.Original\s*\(').Count -ne 1) {
    throw 'Near Assist must own one ordinary and one location-action hook, each with one Original call site.'
}
if ([regex]::Matches($nearAssist, '->UseAction\s*\(').Count -ne 1 -or
    $nearAssist -match '(?-i:\b(ExecuteAction|SendAction|ActionQueued|QueuedAction|QueueAction|RetryAction|RetryDispatch)\b)' -or
    $nearAssist -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Near Assist may own only its reviewed DRK nested call plus the two central Originals; it must never retry, queue, or visibly mutate a target.'
}
if ($normalizedNearAssist -notmatch 'useActionHook!\.Original\s*\(\s*thisPtr\s*,\s*actionType\s*,\s*actionId\s*,\s*forwardedTargetId\s*,\s*extraParam\s*,\s*mode\s*,\s*comboRouteId\s*,\s*outOptAreaTargeted\s*\)') {
    throw 'Near Assist Original must preserve every native action argument except the bounded forwardedTargetId.'
}
if ($normalizedNearAssist -notmatch 'return useActionLocationHook!\.Original\( thisPtr, actionType, actionId, targetId, location, extraParam, a7\);') {
    throw 'The location-action protection detour must forward every native argument unchanged after its fail-open Guard decision.'
}
if ($normalizedNearAssist -notmatch 'if \(!bypassRedirect && shadowbringerCarrier is \{ \} carrier\).*?var safeCarrierPath = !helperTokenConsumed && !targetSuppressedByRedirect && forwardedTargetId == targetId;.*?darkKnightShadowbringer\.TryAttemptOnce\( thisPtr, carrier, safeCarrierPath, IsLocalGuardActiveOrPropagating, \(\) => RunWithoutRedirect\( \(\) => thisPtr->UseAction\( ActionType\.Action, DarkKnightShadowbringerMacroRules\.ShadowbringerActionId, carrier\.EffectiveTargetId, 0, ActionManager\.UseActionMode\.None, 0, null\)\)\);.*?ObserveExactLocalGuardActivationAttempt\(thisPtr, actionType, actionId\);.*?useActionHook!\.Original\(' -or
    [regex]::Matches($nearAssist, '\bdarkKnightShadowbringer\.TryAttemptOnce\s*\(').Count -ne 1 -or
    [regex]::Matches($nearAssist, '\bdarkKnightShadowbringer\.TryConsumePairedCarrier\s*\(').Count -ne 1) {
    throw 'The DRK detour boundary must spend at most one exact Shadowbringer attempt under redirect bypass, then forward the unchanged outer combo carrier through the sole Original.'
}
if ([regex]::Matches($nearAssist, '\bsmartWardensPaean\.Evaluate\s*\(').Count -ne 1 -or
    [regex]::Matches($nearAssist, '\bsmartWardensPaean\.RecordNativeResult\s*\(').Count -ne 1) {
    throw 'The existing shared UseAction hook must contain exactly one Smart Paean evaluation branch and one post-Original client-return recorder.'
}

# The optional CC-immunity brake is a final-target filter inside the already
# reviewed UseAction detour. An exact confirmed block returns false immediately,
# before the sole downstream Original. An unchanged native zero/0xE0000000
# default carrier may be inspected through the stable native hard target, but
# the forwarded action target is never changed. Every Seiton-injected zero is
# explicitly marked and remains unresolved; target-zero policy stays exclusive
# to the reviewed Near/Far carrier paths.
$ccImmunityBrake = Read-RequiredSource $ccImmunityBrakeServicePath 'CC-immunity brake service'
$normalizedCcImmunityBrake = $ccImmunityBrake -replace '\s+', ' '
$ccImmunityBrakeRules = Read-RequiredSource (Join-Path $coreRoot 'CcImmunityBrakeRules.cs') 'CC-immunity brake rules'
$normalizedCcImmunityBrakeRules = $ccImmunityBrakeRules -replace '\s+', ' '
$ccImmunityBrakeTargetRules = Read-RequiredSource $ccImmunityBrakeTargetRulesPath 'CC-immunity brake target rules'
$normalizedCcImmunityBrakeTargetRules = $ccImmunityBrakeTargetRules -replace '\s+', ' '
$ccImmunityBrakeMetadata = Read-RequiredSource $ccImmunityBrakeMetadataGuardPath 'CC-immunity brake metadata guard'

$ccBrakeTypeReferences = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bCcImmunityBrakeService\b')
$unexpectedCcBrakeReferences = @($ccBrakeTypeReferences | Where-Object {
    $_.Path -notin @($pluginPath, $nearAssistPath, $ccImmunityBrakeServicePath)
})
if ($unexpectedCcBrakeReferences.Count -gt 0) {
    $locations = $unexpectedCcBrakeReferences | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "The CC brake may be constructed once and consulted only by the shared action detour: $($locations -join ', ')"
}
if ([regex]::Matches($pluginSource, '\bnew\s+CcImmunityBrakeService\s*\(').Count -ne 1 -or
    [regex]::Matches($nearAssist, '\bccImmunityBrake\.ShouldBlock\s*\(').Count -ne 1 -or
    [regex]::Matches($nearAssist, '\bccImmunityBrake\.RecordFailedOpen\s*\(').Count -ne 1) {
    throw 'The CC brake must have one service instance, one final-target decision site, and one exception pass-through recorder.'
}

$useActionDetourMatch = [regex]::Match(
    $nearAssist,
    '(?s)private bool UseActionDetour\(.*?\r?\n    \}\r?\n\r?\n    private ulong TryResolveRedirect')
if (-not $useActionDetourMatch.Success) {
    throw 'The shared UseAction detour could not be isolated for CC-brake ordering review.'
}
$useActionDetour = $useActionDetourMatch.Value
$normalizedUseActionDetour = $useActionDetour -replace '\s+', ' '
$guardAttemptObserverMatch = [regex]::Match(
    $nearAssist,
    '(?s)private void ObserveExactLocalGuardActivationAttempt\(.*?\r?\n    \}\r?\n\r?\n    private bool TryBlockOwnedAutoGuardCancellation')
$guardStateObserverMatch = [regex]::Match(
    $nearAssist,
    '(?s)private bool IsLocalGuardActiveOrPropagating\(.*?\r?\n    \}\r?\n\r?\n    private ulong TryResolveRedirect')
if (-not $guardAttemptObserverMatch.Success) {
    throw 'The exact local Guard-attempt observer could not be isolated for safety review.'
}
$guardAttemptObserver = $guardAttemptObserverMatch.Value
$normalizedGuardAttemptObserver = $guardAttemptObserver -replace '\s+', ' '
if ($normalizedUseActionDetour -notmatch 'ObserveExactLocalGuardActivationAttempt\(thisPtr, actionType, actionId\); var clientAccepted = useActionHook!\.Original\(.*?forwardedTargetId.*?\); smartWardensPaean\.RecordNativeResult\(smartPaeanResult, clientAccepted\);.*?return clientAccepted;' -or
    $normalizedGuardAttemptObserver -notmatch 'ResolveActionId\(actionManager, actionType, actionId\) != EnemyCombatConstants\.GuardActionId.*?var local = objectTable\.LocalPlayer; if \(!IsLivePlayer\(local\) \|\| DefensiveUtilityProbe\.HasActiveGuard\(local\)\) return; var attempt = new LocalGuardActionAttempt\( clientState\.TerritoryType, local!\.GameObjectId, local\.EntityId, Environment\.TickCount64, 0\); lock \(guardAttemptGate\).*?localGuardActionAttemptGeneration = localGuardActionAttemptGeneration == long\.MaxValue \? 1 : localGuardActionAttemptGeneration \+ 1; latestLocalGuardActionAttempt = attempt with \{ Generation = localGuardActionAttemptGeneration, \};' -or
    $normalizedNearAssist -notmatch 'TryGetRecentExactLocalGuardAttempt\( uint territoryId, ulong localGameObjectId, uint localEntityId, long nowMilliseconds, long maximumAgeMilliseconds, out long observedAtMilliseconds\).*?attempt\.TerritoryId != territoryId \|\| attempt\.LocalGameObjectId != localGameObjectId \|\| attempt\.LocalEntityId != localEntityId.*?nowMilliseconds - attempt\.ObservedAtMilliseconds >= maximumAgeMilliseconds.*?observedAtMilliseconds = attempt\.ObservedAtMilliseconds; return true;') {
    throw 'The detour must observe exact Guard 29054 immediately before its sole Original and expose it only to the same live local identity in the same territory within the bounded age.'
}
if (-not $guardStateObserverMatch.Success -or
    ($guardStateObserverMatch.Value -replace '\s+', ' ') -notmatch 'var local = objectTable\.LocalPlayer; if \(!IsLivePlayer\(local\)\) return false; if \(DefensiveUtilityProbe\.HasActiveGuard\(local\)\) return true; return TryGetRecentExactLocalGuardAttempt\( clientState\.TerritoryType, local!\.GameObjectId, local\.EntityId, Environment\.TickCount64, DefensiveUtilityRules\.GuardPropagationLatchMilliseconds, out _\);.*?catch.*?return true;') {
    throw 'Smart Paean own-Guard suppression must use exact live Guard or the bounded same-identity/territory propagation latch and fail closed on an uncertain Guard view.'
}
$nearAssistBranchIndex = $normalizedUseActionDetour.IndexOf('TryConsumeEligibleToken(')
$nearHelpBranchIndex = $normalizedUseActionDetour.IndexOf('TryConsumeEligibleHelpToken(')
$farHelpBranchIndex = $normalizedUseActionDetour.IndexOf('TryConsumeEligibleFarHelpToken(')
$smartPaeanBranchIndex = $normalizedUseActionDetour.IndexOf('smartWardensPaean.Evaluate(')
$smartPaeanSuppressIndex = $normalizedUseActionDetour.IndexOf('if (smartPaeanResult.ShouldSuppress) return false;')
$smartPaeanRedirectIndex = $normalizedUseActionDetour.IndexOf('if (smartPaeanResult.ShouldRedirect)', [Math]::Max(0, $smartPaeanSuppressIndex))
if ($nearAssistBranchIndex -lt 0 -or
    $nearHelpBranchIndex -le $nearAssistBranchIndex -or
    $farHelpBranchIndex -le $nearHelpBranchIndex -or
    $smartPaeanBranchIndex -le $farHelpBranchIndex -or
    $smartPaeanSuppressIndex -le $smartPaeanBranchIndex -or
    $smartPaeanRedirectIndex -le $smartPaeanSuppressIndex -or
    $normalizedUseActionDetour -notmatch 'if \(!bypassRedirect && !helperTokenConsumed && !targetSuppressedByRedirect && forwardedTargetId == targetId\) \{ smartPaeanResult = smartWardensPaean\.Evaluate\( thisPtr, actionType, actionId, targetId, mode, IsLocalGuardActiveOrPropagating\(\)\); if \(smartPaeanResult\.ShouldSuppress\) return false; if \(smartPaeanResult\.ShouldRedirect\) forwardedTargetId = smartPaeanResult\.ForwardTargetId; \}') {
    throw 'Smart Paean must run once only after Near Assist, Near Help, and Far Help decline ownership; internal bypass, consumed/suppressed carriers, changed targets, and own Guard must prevent its passive redirect.'
}
if ($guardAttemptObserver -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|Original|Replay|Retry|Dispatch|Queue)\b|\bforwardedTargetId\b|\btargetId\b|\breturn\s+false\b|(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Guard propagation observation must remain read-only: it may not suppress, replay, dispatch, queue, or retarget the incoming action.'
}
$brakeDecisionIndex = $normalizedUseActionDetour.IndexOf('ccImmunityBrake.ShouldBlock(')
$brakeFailureIndex = $normalizedUseActionDetour.IndexOf('ccImmunityBrake.RecordFailedOpen(exception)')
$detourOriginalIndex = $normalizedUseActionDetour.IndexOf('useActionHook!.Original(')
$lastRedirectCatchLogIndex = $normalizedUseActionDetour.LastIndexOf('LogFailure(', $brakeDecisionIndex)
if ($brakeDecisionIndex -lt 0 -or
    $brakeFailureIndex -le $brakeDecisionIndex -or
    $detourOriginalIndex -le $brakeFailureIndex -or
    $lastRedirectCatchLogIndex -lt 0 -or
    $lastRedirectCatchLogIndex -ge $brakeDecisionIndex) {
    throw 'The CC brake must run after redirect resolution and its catch, then before the detour''s sole Original call.'
}
if ($normalizedUseActionDetour -notmatch 'try \{ var resolvedActionId = ResolveActionId\(thisPtr, actionType, actionId\); if \(ccImmunityBrake\.ShouldBlock\( actionType, resolvedActionId, targetId, forwardedTargetId, targetSuppressedByRedirect, mode\)\) \{ return false; \} \} catch \(Exception exception\) \{ ccImmunityBrake\.RecordFailedOpen\(exception\); \}') {
    throw 'The unconditional final brake must inspect original and final target identities, return false before Original only on an exact Block decision, and let pass/fail-open paths reach the sole Original.'
}
$brakeDetourSection = [regex]::Match(
    $normalizedUseActionDetour,
    'try \{ var resolvedActionId = ResolveActionId.*?ccImmunityBrake\.RecordFailedOpen\(exception\); \}').Value
if ([regex]::Matches($brakeDetourSection, '\breturn false;').Count -ne 1 -or
    $brakeDetourSection -match '\bbypassRedirect\b' -or
    $brakeDetourSection -match 'forwardedTargetId\s*=\s*InvalidCarrierTargetId|useActionHook!\.Original\s*\(' -or
    $brakeDetourSection -notmatch 'catch \(Exception exception\) \{ ccImmunityBrake\.RecordFailedOpen\(exception\); \}') {
    throw 'Redirect bypass must not skip the final brake: a confirmed block must make zero Original calls via one direct false return, never use target-zero suppression, and exceptions must fail open without changing the final target.'
}
$directZeroSuppressions = [regex]::Matches(
    $normalizedUseActionDetour,
    'forwardedTargetId = InvalidCarrierTargetId; targetSuppressedByRedirect = true;')
$conditionalZeroSuppressions = [regex]::Matches(
    $normalizedUseActionDetour,
    'forwardedTargetId = consumedFallbackCarrier \? InvalidCarrierTargetId : targetId; targetSuppressedByRedirect = consumedFallbackCarrier;')
$allDirectZeroAssignments = [regex]::Matches(
    $normalizedUseActionDetour,
    'forwardedTargetId = InvalidCarrierTargetId;')
$allConditionalZeroAssignments = [regex]::Matches(
    $normalizedUseActionDetour,
    'forwardedTargetId = consumedFallbackCarrier \? InvalidCarrierTargetId : targetId;')
if ($normalizedUseActionDetour -notmatch 'var targetSuppressedByRedirect = false;' -or
    $directZeroSuppressions.Count -ne $allDirectZeroAssignments.Count -or
    $conditionalZeroSuppressions.Count -ne $allConditionalZeroAssignments.Count -or
    $allDirectZeroAssignments.Count -ne 6 -or
    $allConditionalZeroAssignments.Count -ne 2) {
    throw 'Every reviewed Smart Target/Near/Far path that can author target zero must set explicit targetSuppressedByRedirect provenance before the CC brake inspects the call.'
}

Assert-Literals $ccImmunityBrake @(
    'CcImmunityBrakeMetadataGuard.Validate(dataManager, log)',
    'verifiedActionIds = metadata.VerifiedActionIds',
    'verifiedStatusIds = metadata.VerifiedStatusIds',
    'configuration.Enabled',
    'configuration.EnableCcImmunityBrake',
    'verifiedActionIds.Contains(resolvedActionId)',
    'ResolveContext() != SupportedPvPContext.CrystallineConflict',
    'TryResolveExactCanonicalEnemy(',
    'PvPMatchRules.IsPublicCrystallineConflictTerritory(clientState.TerritoryType)',
    'partyEntityIds.Count == 5',
    'partyEntityIds.Contains(localPlayer!.EntityId)',
    'partyEntityIds.IsSubsetOf(visibleEntityIds)',
    'EnemySlotRules.CanUseResolvedEnemy(',
    'HasValidNativeIdentity(candidate!)',
    'candidate.GameObjectId == localPlayer!.GameObjectId',
    'candidate.EntityId == localPlayer.EntityId',
    'partyEntityIds.Contains(candidate.EntityId)',
    'StatusFlags.PartyMember | StatusFlags.AllianceMember',
    'candidate.ClassJob.IsValid',
    'EnemySlotRules.FirstSlot',
    'EnemySlotRules.LastSlot',
    'EnemySlotResolver.Resolve(objectTable, slot)',
    'seenIdentities.Add(identity)',
    'matches.Count != 1',
    'objectTable.SearchByEntityId(match.Player.EntityId) as IPlayerCharacter',
    'tableCandidate.Address != match.Player.Address',
    'tableCandidate.GameObjectId != match.Player.GameObjectId',
    'tableCandidate.EntityId != match.Player.EntityId',
    'target?.StatusList',
    '.Where(verifiedStatusIds.Contains)',
    'CcImmunityBrakeRules.Evaluate(',
    'configuration.IsCcBrakeJobEnabled(localJobId)',
    'configuration.IsCcBrakeActionEnabled(resolvedActionId)',
    'CcImmunityBrakeTargetRules.IsDefaultTargetCarrier(forwardedTargetId)',
    'forwardedTargetId == originalTargetId',
    '!targetSuppressedByRedirect',
    'CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(',
    'targetSuppressedByRedirect)',
    'GetNativeHardTargetId(localPlayer)',
    'character->GetTargetId().Id',
    'GetNativeHardTargetId(localPlayer) == nativeHardTargetId',
    'targetResolution.StartsWith("Exact canonical enemy", StringComparison.Ordinal)',
    'Exact canonical enemy via hostile flag',
    'Exact canonical enemy via complete public-CC party fallback',
    'effectiveTargetId,',
    'bool Configured',
    'bool ActiveInCurrentContext',
    'long DefaultTargetResolutions',
    'long ExactTargetResolutions',
    'long TargetResolutionFailures',
    'ulong LastOriginalTargetId',
    'ulong LastForwardedTargetId',
    'ulong LastEffectiveTargetId',
    'uint LastMode',
    'bool LastTargetSuppressedByRedirect',
    'string LastTargetResolution',
    'string LastSampledStatuses',
    'includeWolvesDenTesting: false',
    'actionType is ActionType.Action or ActionType.PvPAction',
    'ActionManager.UseActionMode.None',
    'ActionManager.UseActionMode.Macro',
    'ActionManager.UseActionMode.Queue',
    '(uint)mode == 100'
) 'Exact, live, canonical CC-immunity brake runtime'
Assert-Literals $ccImmunityBrakeTargetRules @(
    'DefaultTargetSentinel = 0xE0000000UL',
    'if (IsConcreteActorId(forwardedTargetId)) return forwardedTargetId',
    'if (targetSuppressedByRedirect) return 0',
    'var isNativeDefaultTarget = forwardedTargetId is 0 or DefaultTargetSentinel',
    'forwardedTargetId != originalTargetId',
    'return IsConcreteActorId(nativeHardTargetId) ? nativeHardTargetId : 0',
    'targetId is 0 or DefaultTargetSentinel',
    'targetId is not 0 and not DefaultTargetSentinel and not ulong.MaxValue'
) 'Exact default-target carrier resolution'
if ($normalizedCcImmunityBrakeTargetRules -notmatch 'bool targetSuppressedByRedirect = false\) \{ if \(IsConcreteActorId\(forwardedTargetId\)\) return forwardedTargetId; if \(targetSuppressedByRedirect\) return 0; var isNativeDefaultTarget = forwardedTargetId is 0 or DefaultTargetSentinel; if \(!isNativeDefaultTarget \|\| forwardedTargetId != originalTargetId\) return 0; return IsConcreteActorId\(nativeHardTargetId\) \? nativeHardTargetId : 0;' -or
    $normalizedCcImmunityBrakeTargetRules -notmatch 'public static bool IsDefaultTargetCarrier\(ulong targetId\) => targetId is 0 or DefaultTargetSentinel;') {
    throw 'Only an unchanged native zero/0xE0000000 carrier may resolve through the native hard target; explicit redirect suppression provenance must keep every plugin-authored zero unresolved.'
}
$ccImmunityBrakeSelfTests = Read-RequiredSource (
    Join-Path $resolvedRoot 'tests\SeitonSense.Core.SelfTest\CcImmunityBrakeSelfTests.cs') 'CC-immunity brake self-tests'
$normalizedCcImmunityBrakeSelfTests = $ccImmunityBrakeSelfTests -replace '\s+', ' '
$coreSelfTestProgram = Read-RequiredSource (
    Join-Path $resolvedRoot 'tests\SeitonSense.Core.SelfTest\Program.cs') 'Core self-test registry'
if ($normalizedCcImmunityBrakeSelfTests -notmatch 'Equal\( \(ulong\)ExactTarget\.EntityId, CcImmunityBrakeTargetRules\.ResolveEffectiveTargetId\(0, 0, ExactTarget\.EntityId\), "native raw-zero carrier resolves exact hard target"\)' -or
    $normalizedCcImmunityBrakeSelfTests -notmatch 'Equal\( 0UL, CcImmunityBrakeTargetRules\.ResolveEffectiveTargetId\( 0, 0, ExactTarget\.GameObjectId, targetSuppressedByRedirect: true\), "explicit suppression provenance keeps raw zero inert"\)' -or
    $coreSelfTestProgram -notmatch 'CcImmunityBrakeSelfTests\.DefaultTargetCarrierResolvesOnlyTheNativeHardTarget') {
    throw 'Core self-tests must execute both raw 0-to-0 cases: unsuppressed resolves the exact native hard target, while explicit redirect suppression remains inert.'
}
if ($normalizedCcImmunityBrakeSelfTests -notmatch 'ExactMiracleUsesTheSharedFinalDecision\(\).*?exact plugin-owned Miracle remains eligible for the shared final brake.*?exact internal Miracle also respects VPR Hardened Scales' -or
    $coreSelfTestProgram -notmatch 'CcImmunityBrakeSelfTests\.ExactMiracleUsesTheSharedFinalDecision') {
    throw 'Core self-tests must keep an exact plugin-owned Miracle eligible for the same final Resilience and Hardened Scales brake decision.'
}
if ($normalizedCcImmunityBrake -notmatch 'for \(var slot = EnemySlotRules\.FirstSlot; slot <= EnemySlotRules\.LastSlot; slot\+\+\).*?EnemySlotResolver\.Resolve\(objectTable, slot\)' -or
    $normalizedCcImmunityBrake -notmatch 'targetId == candidate\.GameObjectId \|\| targetId == candidate\.EntityId' -or
    $normalizedCcImmunityBrake -notmatch 'var liveStatuses = target\?\.StatusList \.Select\(static status => status\.StatusId\) \.Where\(verifiedStatusIds\.Contains\) \.ToArray\(\)') {
    throw 'CC-brake target resolution must scan exact e1-e5 identities and sample the resolved actor''s live StatusList at action time.'
}
if ($normalizedCcImmunityBrake -notmatch 'var partyEntityIds = partyList \.Select\(static member => member\.EntityId\) \.Where\(IsNetworkEntityId\) \.ToHashSet\(\); var visibleEntityIds = objectTable\.PlayerObjects \.OfType<IPlayerCharacter>\(\) \.Select\(static player => player\.EntityId\) \.Where\(IsNetworkEntityId\) \.ToHashSet\(\); var completePublicCcPartyFallback = PvPMatchRules\.IsPublicCrystallineConflictTerritory\(clientState\.TerritoryType\) && partyEntityIds\.Count == 5 && partyEntityIds\.Contains\(localPlayer!\.EntityId\) && partyEntityIds\.IsSubsetOf\(visibleEntityIds\);' -or
    $normalizedCcImmunityBrake -notmatch '!EnemySlotRules\.CanUseResolvedEnemy\( isSelf, isPartyOrAllianceMember, hasHostileFlag, completePublicCcPartyFallback, !candidate!\.IsDead && candidate\.CurrentHp > 0, candidate\.IsTargetable, candidate\.CurrentHp, candidate\.MaxHp\)' -or
    $normalizedCcImmunityBrake -match '\(candidate\.StatusFlags & StatusFlags\.Hostile\) == 0') {
    throw 'A missing hostile flag may be replaced only by an exact five-member visible local-party proof on a public CC territory; the shared enemy rule must still enforce self, ally, liveness, targetability, and valid HP gates.'
}
$shouldBlockMethod = [regex]::Match(
    $normalizedCcImmunityBrake,
    'internal bool ShouldBlock\(.*?\) \{(?<Body>.*?)\} internal void RecordFailedOpen')
if (-not $shouldBlockMethod.Success) {
    throw 'The one-attempt CC-brake decision method could not be isolated.'
}
$ccBrakeDecisionBody = $shouldBlockMethod.Groups['Body'].Value
$firstNativeHardTargetRead = $ccBrakeDecisionBody.IndexOf('GetNativeHardTargetId(localPlayer)')
$liveStatusSample = $ccBrakeDecisionBody.IndexOf('var liveStatuses = target?.StatusList')
$stableNativeHardTargetRead = $ccBrakeDecisionBody.IndexOf(
    'GetNativeHardTargetId(localPlayer) == nativeHardTargetId',
    [Math]::Max(0, $firstNativeHardTargetRead + 1))
$ccBrakeRuleEvaluation = $ccBrakeDecisionBody.IndexOf('CcImmunityBrakeRules.Evaluate(')
if ($firstNativeHardTargetRead -lt 0 -or
    $liveStatusSample -le $firstNativeHardTargetRead -or
    $stableNativeHardTargetRead -le $liveStatusSample -or
    $ccBrakeRuleEvaluation -le $stableNativeHardTargetRead -or
    $ccBrakeDecisionBody -notmatch 'if \(!hardTargetStable\) \{ exactTarget = false; targetResolution = "Native hard target changed during evaluation"; \}') {
    throw 'Default-carrier braking must re-read and prove the same native hard target after live status sampling and before the pure block decision.'
}
if ($ccBrakeDecisionBody -cmatch '\b(Environment\.TickCount64|DateTime|Stopwatch|RemainingTime|ProtectionMissingGrace|Snapshot|TargetPressureTracker|Task|Timer|Thread|ConcurrentQueue|Queue<|Replay|Retry|Dispatch)\b' -or
    $ccImmunityBrake -match '(?:->|\.)UseAction\s*\(' -or
    $ccImmunityBrake -cmatch '\b(Hook<|HookFromAddress|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event)\b' -or
    $ccImmunityBrake -match '\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'The CC brake must remain a live one-attempt filter with no tracker grace, expiry prediction, replay, native action call, extra hook, input injection, or target setter.'
}
if ($ccBrakeDecisionBody -match '(?m)(?:^|;)\s*forwardedTargetId\s*=') {
    throw 'CC-brake inspection must never change the forwarded target; only the pre-existing Near/Far redirect policy may do so.'
}
Assert-Literals $pluginSource @(
    'cc-brake[configured={ccBrake.Configured},active={ccBrake.ActiveInCurrentContext}',
    'fail-open={ccBrake.FailedOpenAttempts},default={ccBrake.DefaultTargetResolutions}',
    'exact={ccBrake.ExactTargetResolutions},resolve-fail={ccBrake.TargetResolutionFailures}',
    'target={ccBrake.LastOriginalTargetId:X}/{ccBrake.LastForwardedTargetId:X}/',
    '{ccBrake.LastEffectiveTargetId:X},suppressed={ccBrake.LastTargetSuppressedByRedirect}',
    'resolve={ccBrake.LastTargetResolution}',
    'sample={ccBrake.LastSampledStatuses},last={ccBrake.LastEvent}'
) 'Persistent CC-brake target-resolution diagnostics'
Assert-Literals $ccImmunityBrakeRules @(
    'MasterDisabled',
    'JobDisabled',
    'ActionDisabled',
    'ActionNotCataloged',
    'JobMismatch',
    'TargetNotResolvedExactly',
    'InvalidTargetIdentity',
    'IncomingTargetMismatch',
    'NoVerifiedBlocker',
    'VerifiedBlocker',
    'CcImmunityBrakeActionCatalog.TryGet(actionId, out var action)',
    'localJobId != action.JobId',
    '!targetIdentityResolvedExactly',
    '!resolvedTarget.IsValid',
    '!IsExactIncomingTarget(incomingTargetId, resolvedTarget)',
    'CcImmunityBrakeActionCatalog.IsBlockerStatus(',
    'incomingTargetId == target.GameObjectId || incomingTargetId == target.EntityId'
) 'Stateless exact-target CC-immunity brake decision'
if ($ccImmunityBrakeRules -cmatch '\b(Environment\.TickCount64|DateTime|Stopwatch|RemainingTime|Task|Timer|Thread|ConcurrentQueue|Queue<|UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|Replay|Retry|Dispatch)\b') {
    throw 'Pure CC-brake rules must own no clock, queue, retry, replay, action call, or target service.'
}
$consumeState = [regex]::Match($nearAssist, 'oneShotState\s*=\s*NearAssistOneShotState\.Initial\s*;')
$originalCall = [regex]::Match($nearAssist, '\buseActionHook!\.Original\s*\(')
if (-not $consumeState.Success -or -not $originalCall.Success -or $consumeState.Index -gt $originalCall.Index) {
    throw 'Near Assist must consume its one-shot state before the sole Original call.'
}
if ($nearAssist -match '\bCanUseActionOnTarget\s*\(') {
    throw 'Near Assist must not restore the transient target-usability prefilter that defeats native macro queuing.'
}
if ([regex]::Matches($nearAssist, '\bmode\s*==\s*ActionManager\.UseActionMode\.None').Count -ne 2 -or
    [regex]::Matches($nearAssist, '\bmode\s*!=\s*ActionManager\.UseActionMode\.Queue').Count -lt 2) {
    throw 'Near Assist may recognize normal-mode Turbo calls only in its two reviewed mode gates, and Queue must remain rejected.'
}
if ($nearAssist -match 'RaptureShellModule|MacroLocked|MacroCurrentLine|MacroLineText') {
    throw 'Near Assist must not restore the live macro-line timing dependency that caused valid Turbo calls to be missed.'
}
if ($normalizedNearAssist -notmatch 'if \(!rewritten && consumedFallbackCarrier\) \{ forwardedTargetId = InvalidCarrierTargetId; targetSuppressedByRedirect = true; \}' -or
    $normalizedNearAssist -notmatch 'forwardedTargetId = consumedFallbackCarrier \? InvalidCarrierTargetId : targetId; targetSuppressedByRedirect = consumedFallbackCarrier;') {
    throw 'A failed or exceptional fallback carrier must be made invalid so the authored <t> fallback can run.'
}
$nearAssistCarrier = Read-RequiredSource (Join-Path $coreRoot 'NearAssistCarrierRules.cs') 'Near Assist carrier rules'
Assert-Literals $nearAssistCarrier @(
    'objectId is not 0 and not InvalidObjectId',
    'incomingTargetId == carrierEnemyGameObjectId',
    'incomingTargetId == carrierEnemyEntityId',
    'currentHardTargetId == carrierEnemyGameObjectId',
    'currentHardTargetId == carrierEnemyEntityId'
) 'Near Assist carrier rules'
if ($normalizedNearAssist -notmatch 'IsEligibleRedirectAction\s*\(\s*thisPtr\s*,\s*actionType\s*,\s*actionId\s*,\s*mode\s*\)\s*&&\s*TryConsumeEligibleToken') {
    throw 'Near Assist must prove a hostile PvP action shape before the one-shot token can be consumed.'
}
if ($normalizedNearAssist -notmatch 'action\.IsPvP\s*&&\s*action\.CanTargetHostile\s*&&\s*!action\.TargetArea\s*&&\s*action\.Range > 0') {
    throw 'Near Assist pre-consumption filtering must reject defensives, non-PvP actions, ground targeting, and zero-range actions.'
}

Assert-Literals $nearAssist @(
    'NearHelpOneShotRules.Arm',
    'NearHelpOneShotRules.Observe',
    'NearHelpCarrierRules.IsFallbackCarrier',
    'PartySlotResolver.Resolve(objectTable, 2)',
    'GetPartySlots()',
    'IsEligibleHelpAction',
    'action.CanTargetParty || action.CanTargetAlly || action.CanTargetAlliance',
    'nearHelpState = NearHelpOneShotState.Initial',
    'nearHelpState = decision.NextState',
    'mode != ActionManager.UseActionMode.Queue',
    'GetActionInRangeOrLoS',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight',
    'configuration.NearHelpPreferIncomingPressure',
    'action.RowId == resolvedActionId',
    'action.CanTargetSelf',
    'pressureTracker.TryGetIncomingAllyPressure(',
    'pressureTracker.HasActiveIncomingAllyPressureView',
    'IsSelf: true',
    'IsActionSelfTargetable: true',
    'hasTrustedPressureView',
    'RunWithoutRedirect<T>',
    '[ThreadStatic]',
    'internalRedirectBypassDepth++',
    'internalRedirectBypassDepth--',
    'finally',
    'var bypassRedirect = internalRedirectBypassDepth > 0',
    'if (!bypassRedirect &&'
) 'Near Help shared redirector'
if ([regex]::Matches($nearAssist, 'if\s*\(!bypassRedirect\s*&&').Count -ne 8) {
    throw 'Plugin-owned direct helper calls must bypass DRK pairing/attempt plus legacy Far Help suppression, Near Assist, Near Help, Far Help, and passive Smart Paean without consuming a macro token or recursively transforming the plugin-owned action.'
}
if ([regex]::Matches($nearAssist, 'if\s*\(!bypassRedirect\s*\)').Count -ne 0) {
    throw 'The redirect bypass may guard only the eight reviewed pair/redirect/transform/attempt branches; it must never wrap or skip the unconditional final CC brake or Auto-Guard protector.'
}
$nearHelpSelection = Read-RequiredSource (Join-Path $coreRoot 'NearHelpSelectionRules.cs') 'Near Help selection rules'
$normalizedNearHelpSelection = $nearHelpSelection -replace '\s+', ' '
Assert-Literals $nearHelpSelection @(
    'candidate.CurrentHp * current.MaximumHp',
    'current.CurrentHp * candidate.MaximumHp',
    'candidate.DistanceSquared.CompareTo(current.DistanceSquared)',
    'candidate.IsExactFriendly',
    '(!candidate.IsSelf || candidate.IsActionSelfTargetable)',
    'candidate.HasValidActionTarget',
    'candidate.HasRangeAndLineOfSight',
    'CriticalHealthPercent = 25',
    'PressureWindowPercentagePoints = 10',
    'UniqueIncomingEnemyPressureCount',
    'hasTrustedPressureView',
    'IsAtOrBelowCriticalHealth(healthAnchor)',
    'PressureViewUntrusted',
    'PressureDataIncomplete',
    'NoPositivePressure',
    'IncomingPressure',
    'IsInsidePressureWindow(candidate, healthAnchor)',
    'IsBetterByPressure',
    '(UInt128)100 * candidate.CurrentHp * healthAnchor.MaximumHp'
) 'Near Help exact self gate and bounded pressure selection rules'
if ($normalizedNearHelpSelection -notmatch 'if \(IsAtOrBelowCriticalHealth\(healthAnchor\)\).*?CriticalHealthAnchor.*?if \(!hasTrustedPressureView\).*?PressureViewUntrusted.*?candidate\.UniqueIncomingEnemyPressureCount is not >= 0.*?PressureDataIncomplete.*?if \(!hasPositivePressure\).*?NoPositivePressure.*?NearHelpSelectionReason\.IncomingPressure') {
    throw 'Near Help must preserve critical lowest-HP priority and fail back to exact HP before using complete positive incoming-pressure data.'
}
$nearHelpOneShot = Read-RequiredSource (Join-Path $coreRoot 'NearHelpOneShotRules.cs') 'Near Help one-shot rules'
Assert-Literals $nearHelpOneShot @(
    'DefaultLifetimeMilliseconds = 750',
    'NearHelpOneShotState.Initial',
    'NearHelpSelectionRules.SelectBest',
    'attempt.IsFallbackCarrier',
    'InvalidFallbackCarrierTargetId'
) 'Near Help one-shot rules'
$nearHelpCarrier = Read-RequiredSource (Join-Path $coreRoot 'NearHelpCarrierRules.cs') 'Near Help carrier rules'
Assert-Literals $nearHelpCarrier @(
    'incomingTargetId == carrierGameObjectId',
    'incomingTargetId == carrierEntityId',
    'currentHardTargetId == carrierGameObjectId',
    'currentHardTargetId == carrierEntityId'
) 'Near Help carrier rules'
if ($normalizedNearAssist -notmatch 'IsEligibleHelpAction\s*\(\s*thisPtr\s*,\s*actionType\s*,\s*actionId\s*,\s*mode\s*\)\s*&&\s*TryConsumeEligibleHelpToken') {
    throw 'Near Help must prove a friendly PvP action shape before its one-shot token can be consumed.'
}
if ($normalizedNearAssist -notmatch 'action\.IsPvP\s*&&\s*\(action\.CanTargetParty \|\| action\.CanTargetAlly \|\| action\.CanTargetAlliance\)\s*&&\s*!action\.TargetArea\s*&&\s*action\.Range > 0') {
    throw 'Near Help pre-consumption filtering must require a friendly-capable PvP action with native range and no ground targeting.'
}
if ($normalizedNearAssist -notmatch 'var isActionSelfTargetable = supportedAction && action\.RowId == resolvedActionId && action\.CanTargetSelf;.*?if \(isActionSelfTargetable\).*?exactLocal\.GameObjectId, exactLocal\.EntityId.*?GetActionInRangeOrLoS\( resolvedActionId, sourceObject, sourceObject\).*?IsSelf: true,.*?IsActionSelfTargetable: true' -or
    $normalizedNearAssist -notmatch 'var hasTrustedPressureView = preferIncomingPressure && pressureTracker\.HasActiveIncomingAllyPressureView;.*?NearHelpOneShotRules\.Observe\( previousState, attempt, candidates, preferIncomingPressure, hasTrustedPressureView\)') {
    throw 'Near Help self selection must require exact resolved self metadata and native reachability, while pressure trust comes only from the active atomic view and is delegated to pure window-local rules.'
}
if ([regex]::Matches($nearAssist, '\bpressureTracker\.TryGetIncomingAllyPressure\s*\(').Count -ne 2 -or
    $normalizedNearAssist -match 'hasTrustedPressureView\s*=.*?\.All\s*\(') {
    throw 'Near Help must query exact GOID/EID pressure for self and ally candidates without globally rejecting unknown data outside the bounded health window.'
}
$helpConsumeState = [regex]::Match($nearAssist, 'nearHelpState\s*=\s*NearHelpOneShotState\.Initial\s*;')
if (-not $helpConsumeState.Success -or $helpConsumeState.Index -gt $originalCall.Index) {
    throw 'Near Help must consume its one-shot state before the sole Original call.'
}

Assert-Literals $nearAssist @(
    'FarHelpOneShotRules.Arm',
    'FarHelpOneShotRules.Observe',
    'FarHelpCarrierRules.IsFallbackCarrier',
    'FarHelpSelectionRules.FirstPartySlot',
    'FarHelpSelectionRules.LastPartySlot',
    'FarHelpSelectionRules.ClassifyPlayableJob(jobId)',
    'IsEligibleFarHelpAction',
    'TryGetFarHelpMovementDefinition',
    'action.AffectsPosition',
    'action.CanTargetParty || action.CanTargetAlly || action.CanTargetAlliance',
    '!action.CanTargetSelf',
    '!areaTargetedAction',
    'action.Range > 0',
    'action.RequiresLineOfSight',
    'action.ClassJob.RowId == expectedJobId',
    'distanceSquared < maximumDistance * maximumDistance',
    'GetActionInRangeOrLoS',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight',
    'farHelpState = FarHelpOneShotState.Initial',
    'farHelpState = decision.NextState',
    'mode != ActionManager.UseActionMode.Queue',
    'carrier=<me>',
    'local.EntityId,',
    'local.GameObjectId,'
) 'Far Help shared redirector'

if ($normalizedNearAssist -notmatch 'IsEligibleFarHelpAction\s*\(\s*thisPtr\s*,\s*actionType\s*,\s*actionId\s*,\s*mode\s*,\s*out var resolvedFarHelpActionId\s*\)\s*&&\s*TryConsumeEligibleFarHelpToken') {
    throw 'Far Help must exact-ID preclassify the movement action before its one-shot token can be consumed.'
}
if ($normalizedNearAssist -notmatch 'IsEligibleFarHelpAction.*?return resolvedActionId != 0 && TryGetFarHelpMovementDefinition\(resolvedActionId, out _, out _\);') {
    throw 'Far Help pre-consumption filtering must use only the exact reviewed resolved-action allowlist, not generic metadata.'
}
foreach ($mapping in @(
    'case 29066:.*?expectedJobId = 19;.*?maximumDistance = float.PositiveInfinity;',
    'case 29261:.*?expectedJobId = 40;.*?maximumDistance = float.PositiveInfinity;',
    'case 29484:.*?expectedJobId = 20;.*?maximumDistance = float.PositiveInfinity;',
    'case 29660:.*?expectedJobId = 25;.*?maximumDistance = float.PositiveInfinity;',
    'case 39184:.*?expectedJobId = 41;.*?maximumDistance = float.PositiveInfinity;')) {
    if ($normalizedNearAssist -notmatch $mapping) {
        throw "Far Help exact movement action/job mapping drifted: $mapping"
    }
}
if ([regex]::Matches($nearAssist, '(?m)^\s*case\s+(?:29066|29261|29484|29660|39184)\s*:').Count -ne 5 -or
    [regex]::Matches($nearAssist, '(?m)^\s*case\s+\d+\s*:').Count -ne 5) {
    throw 'Far Help must retain exactly the five reviewed movement-action cases and no additional action allowlist entry.'
}
if ($normalizedNearAssist -notmatch 'hasActionMetadata && hasExactMovementDefinition && action\.IsPvP && !action\.CanTargetSelf && action\.Range > 0 && action\.RequiresLineOfSight && action\.ClassJob\.RowId == expectedJobId' -or
    $normalizedNearAssist -notmatch 'if \(supportedContext && supportedAction && movementAction && friendlyAction && !areaTargetedAction && actionManager != null && localIdentityValid\)') {
    throw 'Far Help must revalidate complete PvP movement, friendly-target, job, non-self, non-area, positive-range, and LoS metadata after consuming the exact intent.'
}
if ($normalizedNearAssist -notmatch 'case 29066:.*?expectedJobId = 19;.*?maximumDistance = float\.PositiveInfinity;.*?return true;' -or
    $normalizedNearAssist -notmatch 'var rangeResult = hasValidActionTarget \? ActionManager\.GetActionInRangeOrLoS\(resolvedActionId, sourceObject, targetObject\) : uint\.MaxValue;.*?insideActionSpecificLimit && SeitonRangeRules\.HasNativeRangeAndLineOfSight\(rangeResult\)' -or
    $normalizedNearAssist -match 'case 29066:.*?maximumDistance = 10f;') {
    throw 'Guardian Far Help must use the exact action/job allowlist plus native range/LoS with no manual under-10-yalm center-distance cap.'
}

$farHelpSelection = Read-RequiredSource (Join-Path $coreRoot 'FarHelpSelectionRules.cs') 'Far Help selection rules'
Assert-Literals $farHelpSelection @(
    '24 or 28 or 33 or 40',
    '23 or 31 or 38 or 25 or 27 or 35 or 42',
    'Other = 0',
    'RangedOrCaster = 1',
    'Healer = 2',
    'FarHelpAllyRole.Healer',
    'FarHelpAllyRole.RangedOrCaster',
    'FarHelpAllyRole.Other',
    'candidate.Role > current.Role',
    'candidate.DistanceSquared.CompareTo(current.DistanceSquared)',
    'return distance > 0',
    'candidate.IsExactPartyMember',
    '!candidate.IsSelf',
    'candidate.IsTargetable',
    'candidate.HasValidActionTarget',
    'candidate.HasRangeAndLineOfSight',
    'MaximumCanonicalEnemyCount = 5',
    'MinimumBacklineEnemyEdgeClearance = 10f',
    'candidate.HasCompleteCanonicalEnemySnapshot',
    'candidate.CanonicalLiveEnemyCount is >= 1 and <= MaximumCanonicalEnemyCount',
    'candidate.MinimumCanonicalEnemyEdgeDistance > MinimumBacklineEnemyEdgeClearance',
    'var candidateBacklineSafe = IsBacklineSafe(candidate)',
    'var currentBacklineSafe = IsBacklineSafe(current)'
) 'Far Help safe-backline preference, distance, and exact-tie role selection rules'
$normalizedFarHelpSelection = $farHelpSelection -replace '\s+', ' '
if ($normalizedFarHelpSelection -notmatch 'candidate\.HasRangeAndLineOfSight;.*?public static bool IsBacklineSafe' -or
    $normalizedFarHelpSelection -match 'candidate\.HasRangeAndLineOfSight\s*&&\s*IsBacklineSafe') {
    throw 'Far Help action eligibility must remain independent of the optional backline heuristic so unknown/frontline candidates remain valid fallbacks.'
}
if ($normalizedFarHelpSelection -notmatch 'var candidateBacklineSafe = IsBacklineSafe\(candidate\); var currentBacklineSafe = IsBacklineSafe\(current\); if \(candidateBacklineSafe != currentBacklineSafe\) return candidateBacklineSafe; var distance = candidate\.DistanceSquared\.CompareTo\(current\.DistanceSquared\); if \(distance != 0\) return distance > 0; if \(candidate\.Role != current\.Role\) return candidate\.Role > current\.Role;') {
    throw 'Far Help must prefer a safe backline candidate, then compare distance, then use healer/ranged/other only as an exact-distance tie-break.'
}
if ($normalizedFarHelpSelection -notmatch 'candidate\.MinimumCanonicalEnemyEdgeDistance >= 0f && candidate\.MinimumCanonicalEnemyEdgeDistance > MinimumBacklineEnemyEdgeClearance') {
    throw 'Far Help backline safety must require finite nonnegative hitbox-edge clearance strictly greater than 10 yalms.'
}

$farHelpEnemySnapshotMatch = [regex]::Match(
    $nearAssist,
    '(?s)private FarHelpEnemySnapshot ResolveFarHelpEnemySnapshot\(.*?\n    \}\r?\n\r?\n    private static bool TryGetMinimumEnemyEdgeDistance')
if (-not $farHelpEnemySnapshotMatch.Success) {
    throw 'Far Help exact canonical enemy snapshot runtime method is missing.'
}
$farHelpEnemySnapshot = $farHelpEnemySnapshotMatch.Value
$normalizedFarHelpEnemySnapshot = $farHelpEnemySnapshot -replace '\s+', ' '
Assert-Literals $farHelpEnemySnapshot @(
    'EnemySlotRules.FirstSlot',
    'EnemySlotRules.LastSlot',
    'EnemySlotResolver.Resolve(objectTable, slot)',
    'seenEntityIds.Add(enemy.EntityId)',
    'seenGameObjectIds.Add(enemy.GameObjectId)',
    'enemy.IsDead',
    'liveEnemies.Add(new FarHelpEnemyThreat(position.X, position.Z, hitboxRadius))',
    'seenEntityIds.Count == FarHelpSelectionRules.MaximumCanonicalEnemyCount',
    'seenGameObjectIds.Count == FarHelpSelectionRules.MaximumCanonicalEnemyCount'
) 'Far Help exact five-slot canonical enemy snapshot'
if ($farHelpEnemySnapshot -match '\bIsLivePlayer\s*\(|\.IsTargetable\b') {
    throw 'Far Help enemy snapshots must not discard alive untargetable enemies through IsLivePlayer or targetability gates.'
}
if ($normalizedFarHelpEnemySnapshot -notmatch 'if \(enemy\.IsDead\) continue;.*?if \(enemy\.CurrentHp == 0 \|\| enemy\.MaxHp < enemy\.CurrentHp\).*?liveEnemies\.Add') {
    throw 'Far Help must require every exact enemy identity, ignore only confirmed dead enemies for threat distance, and count every unambiguous living enemy including untargetable actors.'
}

$farHelpEdgeDistanceMatch = [regex]::Match(
    $nearAssist,
    '(?s)private static bool TryGetMinimumEnemyEdgeDistance\(.*?\n    \}\r?\n\r?\n    private bool TryGetActionMetadata')
if (-not $farHelpEdgeDistanceMatch.Success) {
    throw 'Far Help horizontal enemy hitbox-edge clearance runtime method is missing.'
}
$farHelpEdgeDistance = $farHelpEdgeDistanceMatch.Value
Assert-Literals $farHelpEdgeDistance @(
    'allyPosition.X - enemy.X',
    'allyPosition.Z - enemy.Z',
    'centerDistance - allyHitboxRadius - enemy.HitboxRadius',
    'MathF.Max(',
    'MathF.Min(minimum, edgeDistance)',
    'float.IsFinite(allyHitboxRadius)',
    'allyHitboxRadius < 0f'
) 'Far Help horizontal XZ hitbox-edge clearance'
if ($farHelpEdgeDistance -match 'allyPosition\.Y|enemy\.Y') {
    throw 'Far Help backline clearance must remain horizontal XZ distance and may not include vertical Y separation.'
}
Assert-Literals $nearAssist @(
    'HasCompleteCanonicalEnemySnapshot: hasCompleteEnemySnapshot',
    'CanonicalLiveEnemyCount: enemySnapshot.LiveEnemies.Length',
    'MinimumCanonicalEnemyEdgeDistance: minimumEnemyEdgeDistance',
    'safe-backline(clearance>10y)',
    'reachable-fallback(snapshot-incomplete)',
    'reachable-fallback(clearance<=',
    'action-valid=',
    'safe-backline='
) 'Far Help action-time backline diagnostics and fallback'

$farHelpOneShot = Read-RequiredSource (Join-Path $coreRoot 'FarHelpOneShotRules.cs') 'Far Help one-shot rules'
Assert-Literals $farHelpOneShot @(
    'DefaultLifetimeMilliseconds = 750',
    'FarHelpOneShotState.Initial',
    'FarHelpSelectionRules.SelectBestIndex',
    'InvalidSuppressedTargetId',
    'NonMovementAction',
    'ConsumedWithoutRewrite',
    'ClearedSuppressed(FarHelpOneShotReason.Expired)',
    'RewriteTarget'
) 'Far Help bounded one-shot and fallback rules'
if ($normalizedNearAssist -notmatch 'handlingFarHelp = true; ArmFarHelpFallbackSuppression\(actionType, actionId, resolvedFarHelpActionId\); forwardedTargetId = TryResolveFarHelpRedirect' -or
    $farHelpOneShot -notmatch 'InvalidSuppressedTargetId') {
    throw 'Far Help must arm legacy same-action suppression before resolving/forwarding and every failed Far Help decision must target zero.'
}
if ($normalizedNearAssist -notmatch 'forwardedTargetId = TryResolveFarHelpRedirect.*?if \(!rewritten\) \{ forwardedTargetId = InvalidCarrierTargetId; targetSuppressedByRedirect = true; \}') {
    throw 'Every claimed Far Help movement call must suppress its target when no redirect was produced, including the exact token-expiry boundary.'
}

$farHelpSuppression = Read-RequiredSource (Join-Path $coreRoot 'FarHelpFallbackSuppressionRules.cs') 'Far Help legacy fallback suppression rules'
Assert-Literals $farHelpSuppression @(
    'DefaultLifetimeMilliseconds = 750',
    'RawActionId',
    'ResolvedActionId',
    'attempt.ResolvedActionId != token.ResolvedActionId',
    'FarHelpFallbackSuppressionDecisionKind.Suppress',
    'previous,'
) 'Far Help bounded legacy same-action suppression rules'
Assert-Literals $nearAssist @(
    'TrySuppressLegacyFarHelpFallback(thisPtr, actionType, actionId, mode)',
    'forwardedTargetId = InvalidCarrierTargetId',
    'ArmFarHelpFallbackSuppression(actionType, actionId, resolvedFarHelpActionId)',
    'farHelpFallbackSuppressionState = decision.NextState',
    'ActionManager.UseActionMode.Queue',
    'Redirect failed closed; movement target suppressed',
    'farHelpFallbackSuppressionState = FarHelpFallbackSuppressionState.Initial'
) 'Far Help no-hostile-fallback runtime quarantine'
$suppressionBranch = $normalizedNearAssist.IndexOf('TrySuppressLegacyFarHelpFallback(thisPtr, actionType, actionId, mode)')
$nearAssistBranch = $normalizedNearAssist.IndexOf('IsEligibleRedirectAction(thisPtr, actionType, actionId, mode)')
if ($suppressionBranch -lt 0 -or $nearAssistBranch -lt 0 -or $suppressionBranch -gt $nearAssistBranch) {
    throw 'Legacy Far Help fallback suppression must run before every ordinary redirect branch.'
}
if ($normalizedNearAssist -notmatch 'if \(failedFarHelp\) \{ forwardedTargetId = InvalidCarrierTargetId;') {
    throw 'Far Help exceptions must suppress the movement target instead of preserving the selected target.'
}

$farHelpCarrier = Read-RequiredSource (Join-Path $coreRoot 'FarHelpCarrierRules.cs') 'Far Help carrier rules'
Assert-Literals $farHelpCarrier @(
    'incomingTargetId == carrierGameObjectId',
    'incomingTargetId == carrierEntityId',
    'currentHardTargetId == carrierGameObjectId',
    'currentHardTargetId == carrierEntityId'
) 'Far Help exact carrier rules'

$farHelpConsumeState = [regex]::Match($nearAssist, 'farHelpState\s*=\s*FarHelpOneShotState\.Initial\s*;')
if (-not $farHelpConsumeState.Success -or $farHelpConsumeState.Index -gt $originalCall.Index) {
    throw 'Far Help must consume its one-shot state before the sole Original call.'
}
if ($normalizedNearAssist -notmatch 'var hadToken = armedTarget is not null \|\| oneShotState\.IsArmed \|\| armedSmartTarget is not null \|\| armedHelpTarget is not null \|\| nearHelpState\.IsArmed \|\| armedFarHelpTarget is not null \|\| farHelpState\.IsArmed \|\| farHelpFallbackSuppressionState\.IsArmed;.*?armedTarget = null;.*?armedSmartTarget = null;.*?armedHelpTarget = null;.*?armedFarHelpTarget = null;.*?farHelpFallbackSuppressionState = FarHelpFallbackSuppressionState\.Initial;') {
    throw 'Near Assist, Smart Target, Near Help, and Far Help tokens must be mutually exclusive and cleared together.'
}
if ($normalizedNearAssist -notmatch 'catch \(Exception exception\) \{ var failedSmartTarget = handlingSmartTarget;.*?failedSmartTarget \|= armedSmartTarget is not null;.*?armedSmartTarget = null;.*?if \(failedSmartTarget\) \{ smartTargetLastEnemySlot = 0; smartTargetLastEvent = "Redirect failed closed; one-shot Smart Target token cleared";') {
    throw 'The shared redirect exception path must detect an in-flight or still-armed Smart Target token, clear it, and publish fail-closed diagnostics.'
}

$partySlotResolver = Read-RequiredSource $partySlotResolverPath 'Party slot resolver'
Assert-Literals $partySlotResolver @(
    'slot is < 1 or > 8',
    'ResolvePlaceholder($"<{slot}>", 1, 0)',
    'objectTable.SearchByEntityId(entityId) as IPlayerCharacter',
    'player.EntityId == entityId',
    'player.Address == (nint)nativeObject'
) 'Exact native party-slot resolver'
if ($partySlotResolver -match '\b(SetTarget|UseAction|UseActionLocation|TargetManager|ITargetManager)\b') {
    throw 'Party slot resolution must remain read-only and may not mutate targets or actions.'
}

$slotResolver = Read-RequiredSource $slotResolverPath 'Enemy slot resolver'
if ($slotResolver -notmatch 'ResolvePlaceholder\(\$"<e\{slot\}>"\s*,\s*1\s*,\s*0\s*\)') {
    throw 'Enemy slots must come from exact native <e1>-<e5> placeholder resolution.'
}
Assert-Literals $slotResolver @(
    'GameMain.Instance',
    'PvPDuelManager.EnemyEntityId',
    'ResolveWolvesDenDuelOpponent',
    'objectTable.SearchByEntityId(nativeEnemyEntityId)'
) 'Native Wolves Den duel identity'

$pvpMatchRules = Read-RequiredSource (Join-Path $coreRoot 'PvPMatchRules.cs') 'Supported PvP context rules'
Assert-Literals $pvpMatchRules @(
    'SupportedPvPContext.CrystallineConflict',
    'SupportedPvPContext.WolvesDen',
    'WolvesDenPierTerritoryId = 250',
    'territoryId == WolvesDenPierTerritoryId'
) 'Supported PvP context rules'
$normalizedPvpMatchRules = $pvpMatchRules -replace '\s+', ' '
if ($normalizedPvpMatchRules -notmatch 'includeWolvesDenTesting\s*&&\s*isPvP\s*&&\s*!isPvPExcludingWolvesDen\s*&&\s*territoryId\s*==\s*WolvesDenPierTerritoryId') {
    throw "Wolves' Den must require opt-in, live PvP, the excluding-Den inverse, and exact territory 250."
}

$wolvesDenRules = Read-RequiredSource (Join-Path $coreRoot 'WolvesDenOpponentRules.cs') 'Wolves Den opponent rules'
Assert-Literals $wolvesDenRules @(
    'ResolveSingleSlot',
    'candidate.MatchesNativeDuelEnemyId',
    'candidate.HasValidAddress',
    '!candidate.IsSelf',
    'candidate.HasHostileFlag',
    'candidate.IsTargetable',
    'EnemySlotRules.FirstSlot'
) 'Wolves Den opponent rules'
$wolvesDenResolver = Read-RequiredSource $wolvesDenResolverPath 'Wolves Den opponent resolver'
Assert-Literals $wolvesDenResolver @(
    'WolvesDenOpponentRules.ResolveSingleSlot',
    'StatusFlags.Hostile',
    'player.IsTargetable',
    'player.Address != 0'
) 'Wolves Den opponent resolver'
if ($wolvesDenResolver -match '\b(StatusFlags\.(PartyMember|AllianceMember)|partyEntityIds)\b') {
    throw "Native duel opponents must not be rejected merely because the players stayed in a party."
}
if ($wolvesDenResolver -match '\b(Write|Set|UseAction|TargetManager|ResolvePlaceholder)\b') {
    throw 'Wolves Den opponent resolver must remain read-only and must not pretend to provide native CC slots.'
}
if ($slotResolver -match '\b(Write|Set|UseAction|TargetManager)\b') {
    throw 'Enemy slot resolver must remain read-only.'
}

$readiness = Read-RequiredSource $readinessPath 'Seiton readiness probe'
Assert-Literals $readiness @(
    'GetAdjustedActionId',
    'GetActionInRangeOrLoS',
    'IsActionOffCooldown',
    'LimitBreakController.Instance',
    'TryGetReadyAction',
    'HasRangeAndLineOfSight',
    'BaseActionId = 29515',
    'FollowUpActionId = 29516',
    'UnsealedStatusId = 3192',
    'MaximumRange = 20f'
) 'Seiton readiness probe'
if ($readiness -match '\b(UseAction|UseActionLocation|ActionQueued|QueuedAction)\b') {
    throw 'Seiton readiness probe must never execute or queue an action.'
}
if ($readiness -match '\b(GetActionStatus|CanUseActionOnTarget)\b') {
    throw 'Seiton readiness must not use transient target/facing/action-lock gates that caused the old flicker.'
}

$namePlateAnchor = Read-RequiredSource $namePlateAnchorPath 'Nameplate anchor'
Assert-Literals $namePlateAnchor @(
    'INamePlateGui',
    'OnDataUpdate',
    'OnPostDataUpdate',
    'GetAddonByName<AddonNamePlate>',
    'NamePlateIndex',
    'NamePlateObjectArray',
    'NamePlateObjectAddress != (nint)plate',
    'NameIcon',
    'GetBounds'
) 'Nameplate anchor'
if ($namePlateAnchor -match '\b(LoadIconTexture|UnloadTexture|ToggleVisibility|SetPosition|SetScale|Destroy)\s*\(') {
    throw 'Nameplate integration must copy bounds only and never mutate native UI nodes.'
}

$inputContext = Read-RequiredSource $inputContextPath 'Game input context probe'
Assert-Literals $inputContext @(
    'IKeyState',
    'GetValidVirtualKeys',
    'RaptureAtkModule.Instance',
    'IsTextInputActive',
    'WantTextInput',
    'PhysicalGameplayKeyRules.Observe',
    'PhysicalGameplayKeyRules.Consume',
    'ConsumeHeldGameplayKeys'
) 'Game input context probe'
if ($inputContext -match 'io\.WantCaptureKeyboard') {
    throw 'Ordinary ImGui keyboard capture must not masquerade as active text input.'
}
if ($inputContext -match '\b(SetRawValue|ClearAll|FireCallback|SendEvent|SetPosition|SetScale|ToggleVisibility)\b') {
    throw 'Game input context probe must remain read-only.'
}

$trackerPath = Join-Path $pluginServicesRoot 'ExecuteTracker.cs'
$tracker = Read-RequiredSource $trackerPath 'Execute tracker'
Assert-Literals $tracker @(
    'ExecuteThreshold.IsBelowHalf',
    'TryGetReadyAction',
    'HasRangeAndLineOfSight',
    'EnemySlotResolver.Resolve',
    'PvPMatchRules.ResolveSupportedContext',
    'configuration.EnableWolvesDenTesting',
    'WolvesDenOpponentResolver.Resolve',
    'context == SupportedPvPContext.CrystallineConflict',
    'context == SupportedPvPContext.WolvesDen',
    'PersistentSeitonCueRules.IsPreparationBand',
    'PersistentSeitonCueRules.Observe',
    'GuardCooldownRules.ObserveStatus',
    'LowMpRules.Observe'
) 'Execute tracker'
$normalizedTracker = $tracker -replace '\s+', ' '
if ($normalizedTracker -notmatch 'isWolvesDen\s*\?\s*\(player\.StatusFlags\s*&\s*StatusFlags\.Hostile\)\s*!=\s*0\s*:\s*!isAlly') {
    throw "Wolves' Den must accept the exact hostile duel opponent even when the players stayed in a party."
}

# The integrated HOWMANY/pressure path may observe exact actor identity, hard/cast
# targets, and bounded ActionEffect evidence. It must never mutate game state.
$targetPressureTracker = Read-RequiredSource $targetPressureTrackerPath 'Target pressure tracker'
$targetPressureSnapshot = Read-RequiredSource $targetPressureSnapshotPath 'Target pressure runtime snapshot'
$coreTargetPressure = Read-RequiredSource (Join-Path $coreRoot 'TargetPressureSnapshot.cs') 'Target pressure core snapshot'
$nearAssistPressureSelection = Read-RequiredSource (Join-Path $coreRoot 'NearAssistPressureSelectionRules.cs') 'Near Assist pressure selection rules'
$pressureCounter = Read-RequiredSource $pressureCounterPath 'Pressure counter window'
Assert-Literals $targetPressureTracker @(
    'UpdateIntervalMilliseconds = 100',
    'clientState.IsPvPExcludingDen',
    'configuration.PressureIncludeWolvesDen',
    'executeTracker.Enemies',
    'CorePressureSnapshot.Build',
    'TargetPressureSources.HardTarget',
    'TargetPressureSources.CastTarget',
    'TargetPressureSources.RecentHarmfulAction',
    'TargetPressureSources.MachinistLimitBreakEarlyMarker',
    'ResolveNativeHardTarget',
    'native->EntityId != player.EntityId',
    'GetTargetId().ObjectId',
    'Snapshot.Find(gameObjectId, entityId)',
    'CcProtectionStatusCatalog.BuildIndicators',
    'indicator.StatusId is 3054 or 3673 ? 3054u : indicator.StatusId',
    'configuration.EnableNearAssistMacro &&',
    'configuration.NearHelpPreferIncomingPressure',
    'HasActiveIncomingAllyPressureView',
    'new TargetPressurePartyAllyObservation(',
    'localIdentity,',
    'localPlayer.IsTargetable',
    'now - state.LastSeenAtMilliseconds >= ProtectionMissingGraceMilliseconds'
) 'Read-only target pressure tracker'
Assert-Literals $coreTargetPressure @(
    'TargetPressureSources.HardTarget',
    'TargetPressureSources.CastTarget',
    'TargetPressureSources.RecentHarmfulAction',
    'TargetPressureSources.MachinistLimitBreakEarlyMarker',
    'ambiguousEnemyIdentities.Contains(observation.Actor)',
    'observation.HardTarget == localPlayer',
    'observation.CastTarget == localPlayer',
    'ally.HardTarget is { } hardTarget',
    'enemies.ContainsKey(ally.HardTarget!.Value)',
    'ally.Actor == localPlayer || !SharesEitherId(ally.Actor, localPlayer)',
    'counts[pair.Value] = counts.GetValueOrDefault(pair.Value) + 1',
    'public int GetTotalTeamTargetCount(',
    'var count = GetAllyTargetCount(enemy)',
    'exactLocalTarget == enemy',
    '? count + 1',
    ': count'
) 'Exact-identity target pressure aggregation'
Assert-Literals $nearAssistPressureSelection @(
    'if (!followTeamPressure)',
    'NearAssistSelectionRules.SelectBestIndex',
    'RolePreferenceWindowYalms',
    'candidate.AllyTargetCount > current.AllyTargetCount',
    'candidate.ExactEnemyTarget.IsValid'
) 'Optional Near Assist pressure preference'
Assert-Literals $pressureCounter @(
    'tracker.Snapshot.Opponents.Where',
    'TargetPressureEvidence',
    'opponent.IsIncoming'
) 'Read-only pressure counter'
Assert-Literals $targetPressureSnapshot @(
    'TargetPressureEvidence IncomingEvidence',
    'int TeamTargetCount',
    'internal int TotalTeamTargetCount { get; init; }',
    'IncomingEvidence != TargetPressureEvidence.None'
) 'Immutable target pressure runtime snapshot with isolated total-team follow-up count'
if ($normalizedTargetPressureTracker -notmatch 'internal int GetTeamTargetCount\(ulong gameObjectId, uint entityId\).*?opponent\.TeamTargetCount' -or
    $normalizedTargetPressureTracker -notmatch 'internal bool TryGetFreshTeamTargetCount\( TargetPressureActorIdentity expectedLocalPlayer, TargetPressureActorIdentity expectedEnemy, long nowMilliseconds, long maximumAgeMilliseconds, out int teamTargetCount\).*?!expectedLocalPlayer\.IsValid.*?!expectedEnemy\.IsValid.*?!current\.Active.*?!current\.PressureActive.*?current\.LocalPlayer != expectedLocalPlayer.*?current\.PublishedAtMilliseconds < 0.*?nowMilliseconds < current\.PublishedAtMilliseconds.*?maximumAgeMilliseconds < 0.*?nowMilliseconds - current\.PublishedAtMilliseconds > maximumAgeMilliseconds.*?current\.Find\(expectedEnemy\.GameObjectId, expectedEnemy\.EntityId\).*?opponent\.TotalTeamTargetCount < 0.*?teamTargetCount = opponent\.TotalTeamTargetCount; return true;' -or
    $normalizedTargetPressureTracker -notmatch 'new TargetPressureOpponentSnapshot\( observation\.Actor\.GameObjectId, observation\.Actor\.EntityId, observation\.JobId, observation\.CcEnemySlot, ToRuntimeEvidence\(sources\), pressureEnabledForContext \? core\.GetAllyTargetCount\(observation\.Actor\) : 0, displays\) \{ TotalTeamTargetCount = pressureEnabledForContext \? core\.GetTotalTeamTargetCount\(observation\.Actor, localHardTarget\) : 0, \}' -or
    $normalizedTargetPressureTracker -notmatch 'result\.Sum\(static enemy => enemy\.TeamTargetCount\)') {
    throw 'The fresh follow-up reader must fail closed on active exact local/enemy identity and snapshot age, return only isolated TotalTeamTargetCount, and leave legacy TeamTargetCount publication/diagnostics ally-only.'
}
$pressureReadOnlySources = @(
    $targetPressureTracker,
    $targetPressureSnapshot,
    $coreTargetPressure,
    $nearAssistPressureSelection,
    $pressureCounter
) -join "`n"
if ($pressureReadOnlySources -match '\b(ActionManager|UseAction|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event|RaptureShellModule|HookFromAddress|Hook<)\b') {
    throw 'Integrated pressure tracking and display must remain read-only and hook-free.'
}
if ($targetPressureTracker -match '->\s*[A-Za-z_]\w*\s*=(?!=)' -or
    $targetPressureTracker -match '\b(Marshal\.Write|Unsafe\.Write|MemoryMarshal\.Write)\b') {
    throw 'The target-pressure native boundary may validate and read actor identity, but may never write native memory.'
}
$missingGrace = [regex]::Match($targetPressureTracker, '\bProtectionMissingGraceMilliseconds\s*=\s*(?<Value>\d+)\s*;')
if (-not $missingGrace.Success -or
    [int]$missingGrace.Groups['Value'].Value -lt 100 -or
    [int]$missingGrace.Groups['Value'].Value -gt 250) {
    throw 'CC-protection missing-frame grace must stay narrowly bounded between 100 and 250 milliseconds.'
}

# The urgent self-pressure path is deliberately narrower than HOWMANY: only the
# fresh, exact, deduplicated hard/cast target union may create a warning episode,
# system sound, or held-movement Sprint intent. Historical hits and the MCH early
# marker remain display evidence only and must never cross this action boundary.
$pressureEscapeRules = Read-RequiredSource $pressureEscapeRulesPath 'Pressure-escape core rules'
$pressureEscapeSelfTests = Read-RequiredSource $pressureEscapeSelfTestsPath 'Pressure-escape core self-tests'
$pressureEscapeSprint = Read-RequiredSource $pressureEscapeSprintProbePath 'Pressure-escape Sprint runtime'
$normalizedPressureEscapeRules = $pressureEscapeRules -replace '\s+', ' '
$normalizedPressureEscapeSprint = $pressureEscapeSprint -replace '\s+', ' '
$pressureEscapeCombatConstants = Read-RequiredSource (Join-Path $pluginServicesRoot 'EnemyCombatConstants.cs') 'Pressure-escape combat constants'
Assert-Literals $pressureEscapeRules @(
    'RequiredDirectEnemyCount = 3',
    'MaximumPressureAgeMilliseconds = 250',
    'WarningClearGraceMilliseconds = 300',
    'bool EpisodeOpen',
    'long SafeSinceMilliseconds',
    'ulong EpisodeToken',
    'pressureKnown && directEnemyCount >= RequiredDirectEnemyCount',
    'var entered = !previous.EpisodeOpen',
    'var token = entered ? NextEpisodeToken(previous.EpisodeToken) : previous.EpisodeToken',
    'previous.SafeSinceMilliseconds',
    'observation.NowMilliseconds - safeSince',
    'current == ulong.MaxValue ? 1UL : current + 1',
    'IsSupportedMovementVirtualKey',
    'AVirtualKey = 0x41',
    'DVirtualKey = 0x44',
    'SVirtualKey = 0x53',
    'WVirtualKey = 0x57'
) 'Exact high-pressure threshold, episode continuity, and movement-key rules'
if ($normalizedPressureEscapeRules -notmatch 'if \(observation\.HardReset\) return Unknown\(PressureEscapeWarningState\.Initial\);' -or
    $normalizedPressureEscapeRules -notmatch 'return Unknown\(new PressureEscapeWarningState\( false, previous\.EpisodeOpen, -1, previous\.EpisodeToken\)\);' -or
    $normalizedPressureEscapeRules -notmatch 'var safeSince = previous\.SafeSinceMilliseconds >= 0 \? previous\.SafeSinceMilliseconds : observation\.NowMilliseconds; var insideClearGrace = observation\.NowMilliseconds - safeSince < WarningClearGraceMilliseconds;' -or
    $normalizedPressureEscapeRules -notmatch 'insideClearGrace \? new PressureEscapeWarningState\( previous\.IsVisible, true, safeSince, previous\.EpisodeToken\) : new PressureEscapeWarningState\( false, false, -1, previous\.EpisodeToken\)') {
    throw 'Unknown/stale pressure must hide without closing or rearming an open episode; only 300 ms of continuously known below-three pressure may close it.'
}
Assert-Literals $pressureEscapeSelfTests @(
    'DirectThresholdIsInclusiveAndUnknownFailsClosed',
    'WarningEntryIsImmediateAndClearIsDebounced',
    'UnknownOrStalePressureClearsImmediately',
    'unknown gap cannot rearm sound or Sprint',
    'unknown gap retains the same episode token',
    'SprintRequiresEveryExactGateAndMovementKey',
    'MovementKeySetIsNarrow',
    'WarningEpisodeTokenWrapsToANonZeroValue',
    'two direct enemies are below threshold',
    'three direct enemies meet threshold',
    'Guard blocks',
    'active Sprint blocks',
    'spent episode blocks',
    'action key blocks'
) 'Executable pressure-episode and held-Sprint safety examples'

Assert-Literals $targetPressureSnapshot @(
    'TargetPressureActorIdentity LocalPlayer',
    'long PublishedAtMilliseconds',
    'DirectSelfPressureSnapshot(',
    'int UniqueEnemyCount',
    'int HardTargetEnemyCount',
    'int CastTargetEnemyCount'
) 'Exact immutable direct-self pressure publication'
Assert-Literals $coreTargetPressure @(
    'var aggregates = new Dictionary<TargetPressureActorIdentity, EnemyAggregate>()',
    'if (ambiguousEnemyIdentities.Contains(observation.Actor)) continue',
    'if (!aggregates.TryGetValue(observation.Actor, out var aggregate))',
    'enemy.Actor.IsValid',
    '!SharesEitherId(enemy.Actor, localPlayer)',
    'enemy.IsHostile',
    '!enemy.IsDead',
    'enemy.IsTargetable'
) 'Unique exact live hostile pressure actors'
$directPressureMethod = [regex]::Match(
    $targetPressureTracker,
    '(?s)internal bool TryGetFreshSelfDirectIncomingPressure\((?<Body>.*?)\r?\n    \}\r?\n\r?\n    ///')
if (-not $directPressureMethod.Success) {
    throw 'The exact fresh direct-self pressure reader is missing.'
}
$directPressureBody = $directPressureMethod.Groups['Body'].Value
Assert-Literals $directPressureBody @(
    'current.LocalPlayer != expectedLocalPlayer',
    'current.PublishedAtMilliseconds < 0',
    'nowMilliseconds < current.PublishedAtMilliseconds',
    'nowMilliseconds - current.PublishedAtMilliseconds > maximumAgeMilliseconds',
    'foreach (var opponent in current.Opponents)',
    'TargetPressureEvidence.HardTarget',
    'TargetPressureEvidence.CastTarget',
    'if (!hardTarget && !castTarget) continue',
    'unique++',
    'new DirectSelfPressureSnapshot('
) 'Fresh exact hard/cast-only self-pressure reader'
if ($directPressureBody -match '\b(IncomingOpponents|IsIncoming|RecentHarmfulAction|MachinistLimitBreakMarker)\b') {
    throw 'High-pressure eligibility must not use the HOWMANY incoming union, recent harmful actions, or the MCH marker.'
}
if ($normalizedTargetPressureTracker -notmatch 'new TargetPressureRuntimeSnapshot\( true, pressureEnabledForContext, localIdentity, Environment\.TickCount64, result\.ToArray\(\)\)') {
    throw 'Each pressure publication must bind exact local identity and a current monotonic timestamp to one immutable opponent set.'
}

Assert-Literals $pressureEscapeCombatConstants @(
    'PvPSprintActionId = 29057',
    'PvPSprintIconId = 9583',
    'PvPSprintRecast100ms = 15',
    'PvPSprintStatusId = 1342',
    'PvPSprintStatusIconId = 210101'
) 'Current reviewed PvP Sprint identifiers'
Assert-Literals $pressureEscapeSprint @(
    'TryGetFreshSelfDirectIncomingPressure(',
    'PressureEscapeRules.MaximumPressureAgeMilliseconds',
    'showWarning || playWarningSound || enableSprintOnHeldMovementKey',
    'playWarningSound && warningDecision.EnteredWarning',
    'Math.Clamp(warningSoundId, 1, 16)',
    'showWarning && warningDecision.WarningActive',
    'inputFrame.HeldMovementKeyEligible',
    'input.HeldMovementKey',
    'warningEpisodeToken != spentSprintEpisodeToken',
    'spentSprintEpisodeToken = frozen.WarningEpisodeToken',
    'inputFrame.Consume()',
    'TryUseSprintOnce(',
    'HeldActionRetryRules.RetainsSchedulerFrame(',
    'HeldActionRetryRules.CanAttemptFrozenIntent(',
    'ClientActionAttemptBoundary.Capture(',
    'ClientActionAttemptBoundaryRules.Classify(',
    'HeldActionRetryRules.Complete(',
    'HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(',
    'HeldActionRetryRules.MaximumNativeAttempts',
    'TargetPressureActorIdentity expectedLocalIdentity',
    'uint expectedTerritoryId',
    'ResolveCurrentContext() != SupportedPvPContext.CrystallineConflict',
    'currentIdentity != expectedLocalIdentity',
    'finalPressure.UniqueEnemyCount',
    'HasActiveStatus(localPlayer!, EnemyCombatConstants.PvPSprintStatusId)',
    'HasEscapeBlockingCrowdControl(localPlayer!)',
    'TryGetRecentExactLocalGuardAttempt(',
    'DefensiveUtilityRules.GuardPropagationLatchMilliseconds',
    'guardSuppression.SuppressDirectActionHelpers',
    'GetAdjustedActionId(EnemyCombatConstants.PvPSprintActionId)',
    'IsActionOffCooldown(',
    'attempted = true',
    'nearAssist.RunWithoutRedirect(() =>',
    'ActionManager.UseActionMode.None',
    'PressureEscapeWarningState.Initial',
    'warningSound.Reset()'
) 'Exact one-episode PvP Sprint runtime boundary with shared bounded retry'
if ([regex]::Matches($pressureEscapeSprint, '\bTryGetFreshSelfDirectIncomingPressure\s*\(').Count -ne 2 -or
    [regex]::Matches($pressureEscapeSprint, '\bUseAction\s*\(').Count -ne 1) {
    throw 'Pressure Sprint must read exact direct pressure once for observation and once at the final boundary, with exactly one native action call site.'
}
$pressureSprintClaim = [regex]::Match($pressureEscapeSprint, '\binputClaimed\s*=\s*true\s*;')
$pressureSprintConsumeInput = [regex]::Match($pressureEscapeSprint, '\binputFrame\.Consume\s*\(\s*\)\s*;')
$pressureSprintFinalRead = [regex]::Match($pressureEscapeSprint, '\bvar outcome\s*=\s*TryUseSprintOnce\s*\(')
$pressureSprintComplete = [regex]::Match($pressureEscapeSprint, '\bCompleteSprintAttempt\s*\(')
if (-not $pressureSprintClaim.Success -or -not $pressureSprintConsumeInput.Success -or
    -not $pressureSprintFinalRead.Success -or -not $pressureSprintComplete.Success -or
    $pressureSprintClaim.Index -gt $pressureSprintConsumeInput.Index -or
    $pressureSprintConsumeInput.Index -gt $pressureSprintFinalRead.Index -or
    $pressureSprintFinalRead.Index -gt $pressureSprintComplete.Index -or
    $normalizedPressureEscapeSprint -notmatch 'if \(completion\.RetryScheduled \|\| completion\.Disposition == HeldActionRetryDisposition\.SoftWait\).*?frozenSprintRetry = frozen with \{ Retry = completion\.NextState \}; return;.*?if \(HeldActionRetryRules\.ShouldLatchHeldKeyUntilRelease\(completion\.Disposition\)\) terminalSprintKey = frozen\.HeldKey; spentSprintEpisodeToken = frozen\.WarningEpisodeToken;') {
    throw 'Pressure Sprint must claim only the current scheduler frame, retain clean-false/soft-wait frozen intent, and spend the pressure episode only on a terminal native result.'
}
$pressureSprintFinalBoundary = [regex]::Match(
    $pressureEscapeSprint,
    '(?s)private unsafe ClientActionAttemptOutcome TryUseSprintOnce\((?<Body>.*?)\r?\n    \}\r?\n\r?\n    private void CompleteSprintAttempt')
if (-not $pressureSprintFinalBoundary.Success) {
    throw 'Pressure Sprint final native boundary is missing.'
}
$pressureSprintFinalBody = $pressureSprintFinalBoundary.Groups['Body'].Value
if ([regex]::Matches($pressureSprintFinalBody, 'clientState\.TerritoryType\s*!=\s*expectedTerritoryId').Count -ne 2 -or
    [regex]::Matches($pressureSprintFinalBody, 'ResolveCurrentContext\(\)\s*!=\s*SupportedPvPContext\.CrystallineConflict').Count -ne 2 -or
    $pressureSprintFinalBody -notmatch 'objectTable\.LocalPlayer' -or
    $pressureSprintFinalBody -notmatch 'TryGetExactLiveIdentity\(localPlayer, out var currentIdentity\)' -or
    $pressureSprintFinalBody -notmatch 'currentIdentity\s*!=\s*expectedLocalIdentity' -or
    $pressureSprintFinalBody -notmatch 'TryGetFreshSelfDirectIncomingPressure\(' -or
    $pressureSprintFinalBody -notmatch 'expectedLocalIdentity\.GameObjectId' -or
    $pressureSprintFinalBody -notmatch 'ActionManager\.UseActionMode\.None') {
    throw 'Immediately before the sole self-Sprint request, exact territory/CC context, local identity, and fresh direct pressure must all be revalidated without target substitution.'
}
$pressureSprintAttempt = [regex]::Match($pressureSprintFinalBody, '\battempted\s*=\s*true\s*;')
$pressureSprintNative = [regex]::Match($pressureSprintFinalBody, '\bactionManager->UseAction\s*\(')
if (-not $pressureSprintAttempt.Success -or -not $pressureSprintNative.Success -or
    $pressureSprintAttempt.Index -gt $pressureSprintNative.Index -or
    $pressureSprintFinalBody -notmatch 'actionManager->UseAction\s*\(\s*ActionType\.Action\s*,\s*EnemyCombatConstants\.PvPSprintActionId\s*,\s*expectedLocalIdentity\.GameObjectId\s*,\s*0\s*,\s*ActionManager\.UseActionMode\.None\s*,\s*0\s*\)') {
    throw 'Pressure Sprint diagnostics must mark an attempt before one exact Action/29057/self/None native request.'
}
Assert-Literals $pressureEscapeSprint @(
    'action.Name.ToString() == "Sprint"',
    'action.Icon == EnemyCombatConstants.PvPSprintIconId',
    'action.IsPvP',
    'action.IsPlayerAction',
    'action.ClassJob.IsValid',
    'action.ClassJob.RowId == 0',
    'action.ClassJobCategory.IsValid',
    'action.ClassJobCategory.RowId == 85',
    'action.ActionCategory.IsValid',
    'action.ActionCategory.RowId == 4',
    'action.CastType == 1',
    'action.Range == 0',
    'action.EffectRange == 0',
    'action.Cast100ms == 0',
    'action.Recast100ms == EnemyCombatConstants.PvPSprintRecast100ms',
    'action.PrimaryCostType == 0',
    'action.PrimaryCostValue == 0',
    'action.SecondaryCostType == 0',
    'action.SecondaryCostValue.RowId == 0',
    'action.CooldownGroup == 58',
    'action.AdditionalCooldownGroup == 0',
    'action.MaxCharges == 0',
    'action.StatusGainSelf.RowId == EnemyCombatConstants.PvPSprintStatusId',
    'action.CanTargetSelf',
    '!action.CanTargetParty',
    '!action.CanTargetAlly',
    '!action.CanTargetAlliance',
    '!action.CanTargetHostile',
    '!action.CanTargetOwnPet',
    '!action.CanTargetPartyPet',
    '!action.TargetArea',
    'action.RequiresLineOfSight',
    'action.NeedToFaceTarget',
    'action.PreservesCombo',
    '!action.AffectsPosition',
    'Increases movement speed by 50%',
    'Effect ends upon reuse or execution of another action',
    'status.Name.ToString() == "Sprint"',
    'status.Icon == EnemyCombatConstants.PvPSprintStatusIconId',
    'status.StatusCategory == 1',
    'status.IsPermanent',
    '!status.CanDispel',
    '!status.LockMovement',
    'Movement speed is increased'
) 'Fail-closed current-sheet PvP Sprint action and status metadata'
$pressureFailClosed = [regex]::Match(
    $pressureEscapeSprint,
    '(?s)internal PressureEscapeSprintProbeSnapshot FailClosed\(.*?\)\s*\{(?<Body>.*?)\r?\n    \}\r?\n\r?\n    private unsafe ClientActionAttemptOutcome TryUseSprintOnce')
if (-not $pressureFailClosed.Success -or
    $pressureFailClosed.Groups['Body'].Value -notmatch 'warningState\.EpisodeOpen' -or
    $pressureFailClosed.Groups['Body'].Value -notmatch 'warningState\.EpisodeToken' -or
    $pressureFailClosed.Groups['Body'].Value -notmatch 'spentSprintEpisodeToken' -or
    $pressureFailClosed.Groups['Body'].Value -match 'warningSound\.Reset|spentSprintEpisodeToken\s*=\s*0|warningState\s*=\s*PressureEscapeWarningState\.Initial') {
    throw 'A transient pressure runtime failure must hide fail-closed while preserving episode, sound ownership, and spent-Sprint ownership.'
}
if ($pressureEscapeSprint -match '\b(RetryAction|RetryDispatch|QueuedAction|QueueAction|PendingDispatch|BufferedDispatch|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event|Hook<|HookFromAddress)\b') {
    throw 'Pressure Sprint must have no alternate, target mutation, input injection, hook, custom queue, or replay path outside the shared bounded retry contract.'
}

$pressureEscapeOverlay = Read-RequiredSource (Join-Path $pluginUiRoot 'OverlayRenderer.cs') 'High-pressure overlay'
$normalizedPressureEscapeOverlay = $pressureEscapeOverlay -replace '\s+', ' '
if ($normalizedPressureEscapeOverlay -notmatch 'var highPressureWarningVisible = HighPressureWarningPreviewEnabled \|\| \(configuration\.Enabled && configuration\.ShowHighPressureWarning && pressureEscape\.WarningActive\); if \(highPressureWarningVisible\)') {
    throw 'The live high-pressure card must be explicitly gated by the plugin master, visual option, and current warning state; preview remains separate.'
}
Assert-Literals $pressureEscapeOverlay @(
    'FOCUSED x{Math.Max(3, directEnemyCount)}',
    '3+ ENEMIES TARGETING YOU',
    'DrawIsolationWarning(now, highPressureWarningVisible)',
    'private static (Vector2 Minimum, Vector2 Maximum) HighPressureWarningBounds()',
    'var cardSize = new Vector2(410f, 108f) * scale',
    'viewport.WorkPos.X + Math.Max(0f, (viewport.WorkSize.X - cardSize.X) * 0.5f)',
    'private void DrawIsolationWarning(long now, bool avoidHighPressureWarning)',
    'var cardSize = new Vector2(342f, 88f) * scale',
    'var maximumTopLeft = Vector2.Max(workMinimum, workMaximum - cardSize)',
    'var topLeft = Vector2.Clamp(',
    'if (RectanglesOverlap(topLeft, bottomRight, pressureMinimum, pressureMaximum))',
    'var below = pressureMaximum.Y + gap',
    'var above = pressureMinimum.Y - gap - cardSize.Y',
    'topLeft.Y = maximumTopLeft.Y',
    'private static bool RectanglesOverlap(',
    'The card never changes position or size. Only its border and alpha'
) 'Config-gated fixed top-center high-pressure warning with narrow-work-area isolation collision fallback'
if ($normalizedPressureEscapeOverlay -notmatch 'if \(avoidHighPressureWarning\) \{ var \(pressureMinimum, pressureMaximum\) = HighPressureWarningBounds\(\); if \(RectanglesOverlap\(topLeft, bottomRight, pressureMinimum, pressureMaximum\)\) \{ var gap = 18f \* ImGuiHelpers\.GlobalScale; var below = pressureMaximum\.Y \+ gap; var above = pressureMinimum\.Y - gap - cardSize\.Y; if \(below <= maximumTopLeft\.Y\) topLeft\.Y = below; else if \(above >= workMinimum\.Y\) topLeft\.Y = above; else topLeft\.Y = maximumTopLeft\.Y; bottomRight = topLeft \+ cardSize; \} \}') {
    throw 'Isolation must retain top-left placement normally and stack/clamp only when its actual scaled bounds overlap the top-center pressure card.'
}
if ($inputContext -notmatch 'PressureEscapeRules\.IsSupportedMovementVirtualKey\(candidateToken\)' -or
    $inputContext -notmatch 'RetainEligibleHeldKeyToken\(\s*\(int\)selectedHeldMovementKey' -or
    $inputContext -notmatch 'HeldMovementKeyEligible' -or
    $emergencyInputCoordinator -notmatch 'HeldMovementKeyEligible') {
    throw 'Pressure Sprint must use the separately derived WASD/arrow movement-key generation, never the numerically first arbitrary held gameplay key.'
}
$pressureDebugStart = $pluginSource.IndexOf('[Seiton Sense] pressure-escape[')
$pressureDebugEnd = if ($pressureDebugStart -ge 0) {
    $pluginSource.IndexOf('[Seiton Sense] guardian-comm[', $pressureDebugStart)
} else {
    -1
}
if ($pressureDebugStart -lt 0 -or $pressureDebugEnd -le $pressureDebugStart -or
    $pluginSource.Substring($pressureDebugStart, $pressureDebugEnd - $pressureDebugStart) -notmatch 'UseActionAttempted' -or
    $pluginSource.Substring($pressureDebugStart, $pressureDebugEnd - $pressureDebugStart) -notmatch 'UseActionAccepted' -or
    $pluginSource.Substring($pressureDebugStart, $pressureDebugEnd - $pressureDebugStart) -match '(?i)\b(landed|server accepted|applied successfully)\b') {
    throw 'Pressure Sprint diagnostics must report attempted/client-accepted source facts and never claim that Sprint landed or applied.'
}

# Full CC immunity is an exact metadata-verified allowlist. One-hit/ambiguous
# wards are deliberately excluded rather than being presented as full immunity.
$ccProtectionCatalog = Read-RequiredSource (Join-Path $coreRoot 'CcProtectionStatusCatalog.cs') 'CC protection catalog'
$ccProtectionKind = Read-RequiredSource (Join-Path $coreRoot 'CcProtectionKind.cs') 'CC protection kinds'
$ccProtectionMetadata = Read-RequiredSource $ccProtectionMetadataGuardPath 'CC protection metadata guard'
Assert-Literals $ccProtectionCatalog @(
    'new(3054, "Guard", 214890, CcProtectionKind.Guard, 4.25f',
    'new(3673, "Guard", 214715, CcProtectionKind.Guard, 4.25f',
    'new(3248, "Resilience", 214891, CcProtectionKind.FullImmunity, 2.25f',
    'new(1303, "Inner Release", 212556, CcProtectionKind.FullImmunity, 15.25f',
    'new(1320, "Meikyo Shisui", 214955, CcProtectionKind.FullImmunity, 3.25f',
    'new(4096, "Hardened Scales", 214992, CcProtectionKind.FullImmunity, 4.25f',
    'new(4477, "Swift", 216678, CcProtectionKind.FullImmunity, 4.25f',
    '!float.IsFinite(observation.RemainingTime)',
    'observation.RemainingTime > entry.Definition.MaximumRemainingTime'
) 'Exact full CC-protection catalog'
Assert-Literals $ccProtectionMetadata @(
    'ClientLanguage.English',
    'row.Value.Icon == definition.IconId',
    'row.Value.StatusCategory == 1',
    '!row.Value.CanDispel',
    '!row.Value.IsPermanent',
    'definition.ExpectedDescriptionFragment',
    'verified.Clear()'
) 'Fail-closed CC-protection metadata validation'
Assert-Literals $targetPressureTracker @(
    '1303 => jobId == 21',
    '1320 => jobId == 34',
    '4096 => jobId == 41',
    '4477 => isLargeScalePvP'
) 'Job- and mode-scoped CC protections'
$catalogDefinitions = [regex]::Matches(
    $ccProtectionCatalog,
    '(?m)^\s*new\s*\(\s*(?<Id>\d+)\s*,\s*"(?<Name>[^"]+)"')
$catalogIds = @($catalogDefinitions | ForEach-Object { [uint32]$_.Groups['Id'].Value } | Sort-Object)
$expectedCatalogIds = @([uint32]1303, [uint32]1320, [uint32]3054, [uint32]3248, [uint32]3673, [uint32]4096, [uint32]4477)
if ($ccProtectionCatalog -match '\bnew\s+CcProtectionDefinition\s*\(' -or
    $catalogDefinitions.Count -ne $expectedCatalogIds.Count -or
    ($catalogIds -join ',') -ne ($expectedCatalogIds -join ',')) {
    throw "CC protection catalog must contain only the seven reviewed full-protection statuses; found $($catalogIds -join ',')."
}
$ambiguousWardNames = @($catalogDefinitions | Where-Object {
    $_.Groups['Name'].Value -in @('Aquaveil', "The Warden's Paean", 'Seraph Flight')
})
if ($ambiguousWardNames.Count -gt 0 -or $ccProtectionKind -match '\b(SingleHitWard|OneHitWard|ReactiveWard)\b') {
    throw 'Aquaveil, Warden''s Paean, Seraph Flight, and other one-hit wards must remain outside the full-immunity catalog.'
}

# The action brake deliberately uses its own per-action blocker matrix. Ordinary
# Purify-removable CC and Miracle of Nature do not share the same immunities.
$ccBrakeCatalog = Read-RequiredSource (Join-Path $coreRoot 'CcImmunityBrakeActionCatalog.cs') 'CC-immunity brake action catalog'
$normalizedCcBrakeCatalog = $ccBrakeCatalog -replace '\s+', ' '
$ccBrakeDefinitions = [regex]::Matches(
    $ccBrakeCatalog,
    '(?m)^\s*new\(\s*(?<Job>[\d_]+)\s*,\s*(?<Action>[\d_]+)\s*,\s*"(?<Name>[^"]+)"\s*,\s*CcImmunityBrakeBlockerFamily\.(?<Family>\w+)\s*\),')
$actualCcBrakeDefinitions = @($ccBrakeDefinitions | ForEach-Object {
    $job = [uint32](($_.Groups['Job'].Value) -replace '_', '')
    $action = [uint32](($_.Groups['Action'].Value) -replace '_', '')
    "$job`:$action`:$($_.Groups['Name'].Value)`:$($_.Groups['Family'].Value)"
})
$expectedCcBrakeDefinitions = @(
    '19:29065:Intervene:StandardPurifyCc',
    '21:29081:Blota:StandardPurifyCc',
    '23:29395:Silent Nocturne:StandardPurifyCc',
    '23:29399:Repelling Shot:StandardPurifyCc',
    '24:29228:Miracle of Nature:Miracle',
    '25:41510:Lethargy:StandardPurifyCc',
    '30:29510:Forked Raiju:StandardPurifyCc',
    '30:29707:Fleeting Raiju:StandardPurifyCc',
    '31:29407:Air Anchor:StandardPurifyCc',
    '33:29244:Gravity II:StandardPurifyCc',
    '33:29248:Gravity II (Double Cast):StandardPurifyCc',
    '34:29535:Mineuchi:StandardPurifyCc'
)
if ($actualCcBrakeDefinitions.Count -ne 12 -or
    ($actualCcBrakeDefinitions -join '|') -ne ($expectedCcBrakeDefinitions -join '|')) {
    throw "CC brake must retain exactly the reviewed 12 action/job/family entries; found $($actualCcBrakeDefinitions -join ', ')."
}
$actualCcBrakeJobs = @($ccBrakeDefinitions | ForEach-Object {
    [uint32](($_.Groups['Job'].Value) -replace '_', '')
} | Sort-Object -Unique)
$expectedCcBrakeJobs = @([uint32]19, [uint32]21, [uint32]23, [uint32]24, [uint32]25, [uint32]30, [uint32]31, [uint32]33, [uint32]34)
if ($actualCcBrakeJobs.Count -ne 9 -or
    ($actualCcBrakeJobs -join ',') -ne ($expectedCcBrakeJobs -join ',')) {
    throw "CC brake must expose exactly the reviewed nine jobs; found $($actualCcBrakeJobs -join ',')."
}

$standardBlockerArray = [regex]::Match(
    $ccBrakeCatalog,
    '(?s)StandardPurifyCcBlockerArray\s*=\s*\[(?<Body>.*?)\];')
$miracleBlockerArray = [regex]::Match(
    $ccBrakeCatalog,
    '(?s)MiracleBlockerArray\s*=\s*\[(?<Body>.*?)\];')
if (-not $standardBlockerArray.Success -or -not $miracleBlockerArray.Success) {
    throw 'CC brake blocker matrices could not be isolated.'
}
$standardBlockerIds = @([regex]::Matches($standardBlockerArray.Groups['Body'].Value, '(?m)^\s*(?<Id>[\d_]+)\s*,') | ForEach-Object {
    [uint32](($_.Groups['Id'].Value) -replace '_', '')
})
$miracleBlockerIds = @([regex]::Matches($miracleBlockerArray.Groups['Body'].Value, '(?m)^\s*(?<Id>[\d_]+)\s*,') | ForEach-Object {
    [uint32](($_.Groups['Id'].Value) -replace '_', '')
})
$expectedStandardBlockers = @([uint32]3054, [uint32]3673, [uint32]3248, [uint32]1303, [uint32]1320, [uint32]4096, [uint32]3143)
$expectedMiracleBlockers = @([uint32]3248, [uint32]1320, [uint32]4096, [uint32]3143, [uint32]3052, [uint32]3162)
if (($standardBlockerIds -join ',') -ne ($expectedStandardBlockers -join ',') -or
    ($miracleBlockerIds -join ',') -ne ($expectedMiracleBlockers -join ',')) {
    throw "CC brake blocker matrices drifted: standard=$($standardBlockerIds -join ','), Miracle=$($miracleBlockerIds -join ',')."
}
foreach ($constraint in @(
    '1_303 => targetJobId == 21',
    '1_320 => targetJobId == 34',
    '4_096 => targetJobId == 41',
    '3_052 => targetJobId == 37',
    '3_162 => targetJobId == 38')) {
    if ($ccBrakeCatalog -notmatch [regex]::Escape($constraint)) {
        throw "CC brake job-scoped blocker constraint drifted: $constraint"
    }
}
$standardConstraintMethod = [regex]::Match(
    $ccBrakeCatalog,
    '(?s)private static bool StandardBlockerMatchesTargetJob.*?\{(?<Body>.*?)\};')
$miracleConstraintMethod = [regex]::Match(
    $ccBrakeCatalog,
    '(?s)private static bool MiracleBlockerMatchesTargetJob.*?\{(?<Body>.*?)\};')
$standardConstraintCases = [regex]::Matches($standardConstraintMethod.Groups['Body'].Value, '(?m)^\s*[\d_]+\s*=>\s*targetJobId\s*==\s*\d+')
$miracleConstraintCases = [regex]::Matches($miracleConstraintMethod.Groups['Body'].Value, '(?m)^\s*[\d_]+\s*=>\s*targetJobId\s*==\s*\d+')
if (-not $standardConstraintMethod.Success -or -not $miracleConstraintMethod.Success -or
    $standardConstraintCases.Count -ne 3 -or $miracleConstraintCases.Count -ne 4 -or
    $miracleConstraintMethod.Groups['Body'].Value -notmatch [regex]::Escape('4_096 => targetJobId == 41') -or
    $normalizedCcBrakeCatalog -notmatch 'StandardPurifyCc => ReadOnlyStandardPurifyCcBlockers.*?Miracle => ReadOnlyMiracleBlockers' -or
    $normalizedCcBrakeCatalog -notmatch 'StandardPurifyCc => StandardPurifyCcBlockers\.Contains\(statusId\) && StandardBlockerMatchesTargetJob\(statusId, targetJobId\).*?Miracle => MiracleBlockers\.Contains\(statusId\) && MiracleBlockerMatchesTargetJob\(statusId, targetJobId\)') {
    throw 'CC brake must retain the exact two blocker families plus three standard and four Miracle job-scoped status constraints.'
}

Assert-Literals $ccImmunityBrakeMetadata @(
    'ClientLanguage.English',
    'CcImmunityBrakeActionCatalog.Definitions',
    'expected.JobId == definition.JobId',
    'actions.TryGetRow(definition.ActionId, out var action)',
    'descriptions.TryGetRow(definition.ActionId, out var transient)',
    'action.RowId == expected.ActionId',
    'action.Icon == expected.IconId',
    'action.ClassJob.RowId == expected.JobId',
    'action.IsPvP',
    'action.CanTargetHostile',
    '!action.CanTargetSelf',
    '!action.TargetArea',
    'action.Range == expected.Range',
    'action.EffectRange == expected.EffectRange',
    'action.CastType == expected.CastType',
    'action.Recast100ms == expected.Recast100ms',
    'expected.DescriptionFragment',
    'status.Icon == expected.IconId',
    'status.StatusCategory == 1',
    '!status.CanDispel',
    '!status.IsPermanent',
    'verifiedActions.Clear()',
    'verifiedStatuses.Clear()'
) 'Fail-open English metadata verification for every CC-brake action and blocker'
$metadataActionExpectations = [regex]::Matches(
    $ccImmunityBrakeMetadata,
    '(?m)^\s*new\(\s*(?<Action>[\d_]+)\s*,\s*(?<Job>[\d_]+)\s*,\s*"(?<Name>[^"]+)"\s*,\s*[\d_]+\s*,\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*"[^"]+"\s*\),')
$metadataStatusExpectations = [regex]::Matches(
    $ccImmunityBrakeMetadata,
    '(?m)^\s*new\(\s*(?<Status>[\d_]+)\s*,\s*"(?<Name>[^"]+)"\s*,\s*[\d_]+\s*,\s*"[^"]+"\s*\),')
if ($metadataActionExpectations.Count -ne 12 -or $metadataStatusExpectations.Count -ne 9) {
    throw 'CC brake metadata must pin exactly 12 actions and the union of nine blocker statuses.'
}
$metadataActionPairs = @($metadataActionExpectations | ForEach-Object {
    "$([uint32](($_.Groups['Job'].Value) -replace '_', '')):$([uint32](($_.Groups['Action'].Value) -replace '_', '')):$($_.Groups['Name'].Value)"
})
$catalogActionPairs = @($ccBrakeDefinitions | ForEach-Object {
    "$([uint32](($_.Groups['Job'].Value) -replace '_', '')):$([uint32](($_.Groups['Action'].Value) -replace '_', '')):$($_.Groups['Name'].Value -replace ' \(Double Cast\)$', '')"
})
if (($metadataActionPairs -join '|') -ne ($catalogActionPairs -join '|')) {
    throw 'CC brake metadata expectations must map one-to-one to the exact action catalog.'
}
$metadataStatusIds = @($metadataStatusExpectations | ForEach-Object {
    [uint32](($_.Groups['Status'].Value) -replace '_', '')
} | Sort-Object)
$matrixStatusIds = @(($expectedStandardBlockers + $expectedMiracleBlockers) | Sort-Object -Unique)
if (($metadataStatusIds -join ',') -ne ($matrixStatusIds -join ',')) {
    throw 'CC brake metadata expectations must cover exactly the union of both blocker matrices.'
}

$overlay = Read-RequiredSource (Join-Path $sourceRoot 'SeitonSense.Plugin\UI\OverlayRenderer.cs') 'Overlay renderer'
Assert-Literals $overlay @(
    'DrawStackedNameplateEmblems(',
    'DrawCcProtectionEmblem(emblemMin, emblemMax, timerMin, timerMax, protection, now)',
    'OrderByDescending(static candidate => candidate.ExpiresAtMilliseconds)',
    'DrawStaticCcChevrons',
    'if (finalRequiredHeight > availableHeight) return',
    'new Vector4(1f, 0.18f, 0.22f, 1f)',
    'Pack(new Vector4(1f, 0.07f, 0.1f, 1f))',
    'private static Vector2 PixelSnap',
    'CcProtectionPreviewEnabled'
) 'Static crossed-CC native-nameplate protection emblem'
Assert-Literals $overlay @(
    'DrawLiveSeitonDecisionStack',
    'MergeLiveSeitonCards',
    'BuildCenteredOffsets',
    'hasPersistentHandoff',
    'LiveSeitonDecisionSource.EntryPopup'
) 'Unified Seiton decision stack'
if ($overlay -match '\bDrawPersistentSeitonCues\b') {
    throw 'Entry popups and persistent Seiton cues must not return to separate centered stacks.'
}

# Low-resource auras are presentation-only. They may copy exact current native
# bounds but must never pulse/mutate an action slot or any other native UI node.
$resourceAuraRules = Read-RequiredSource (Join-Path $coreRoot 'ResourceAuraRules.cs') 'Resource aura rules'
$resourceAuraAnchor = Read-RequiredSource $resourceAuraAnchorPath 'Resource aura native anchor tracker'
$normalizedResourceAuraAnchor = $resourceAuraAnchor -replace '\s+', ' '
Assert-Literals $resourceAuraRules @(
    'LowHp = 1',
    'LowMp = 2',
    'LowHpAndMp = LowHp | LowMp',
    '!observation.Alive',
    'observation.CurrentHp > observation.MaximumHp',
    'observation.CurrentMp > observation.MaximumMp',
    '(ulong)observation.CurrentHp * 100UL <=',
    'observation.MpTrusted && observation.LowMpLatched',
    '(true, true) => ResourceAuraKind.LowHpAndMp'
) 'Fail-closed red, blue, and purple resource classification'
if ($resourceAuraRules -match '\b(UseAction|IGameGui|AtkResNode|SetRawValue|PulseActionBarSlot|ITargetManager|TargetManager)\b') {
    throw 'Pure resource-aura rules must contain no action, native UI, input, or target boundary.'
}

Assert-Literals $resourceAuraAnchor @(
    'CaptureSelfHotbarsForPreview()',
    'CaptureHotbars(localPlayer!, ResourceAuraKind.LowHpAndMp, results)',
    'Dictionary<(ulong GameObjectId, uint EntityId), LowMpState>',
    'new HashSet<(ulong GameObjectId, uint EntityId)>()',
    'var identity = (player.GameObjectId, player.EntityId)',
    '!configuration.Enabled || !configuration.EnableResourceAura || !clientState.IsPvP',
    'manaStates.Clear()',
    'LowMpRules.Observe',
    'var exitThreshold = Math.Clamp(threshold + 300, threshold, 10_000)',
    'manaState.HasTrustedSample',
    'LowMpRules.ShouldShowCrossedIcon(manaState)',
    'GetAddonByName<AddonActionBar>("_ActionBar")',
    '"_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04", "_ActionBar05"',
    '"_ActionBar06", "_ActionBar07", "_ActionBar08", "_ActionBar09"',
    'GetAddonByName<AddonActionCross>("_ActionCross")',
    '"_ActionDoubleCrossL", "_ActionDoubleCrossR"',
    'AddHotbarAnchor(localPlayer, (AddonActionBarBase*)primary, kind, results)',
    'AddHotbarAnchor(localPlayer, (AddonActionBarBase*)bar, kind, results)',
    'AddHotbarAnchor(localPlayer, (AddonActionBarBase*)cross, kind, results)',
    'AddHotbarAnchor(localPlayer, (AddonActionBarBase*)doubleCross, kind, results)',
    'TryGetVisibleActionSlotUnion(actionBar, out var minimum, out var maximum)',
    'MaximumActionBarSlotCount = 32',
    'actionBar->ActionBarSlotVector.First',
    'actionBar->ActionBarSlotVector.Last',
    'sizeof(ActionBarSlot)',
    'vectorCount < slotCount || vectorCount > MaximumActionBarSlotCount',
    'first[index].ComponentDragDrop',
    'dragDrop->AtkComponentBase.OwnerNode',
    'new HashSet<nint>()',
    'TryGetActionSlotBounds(node, addonRoot, out var slotMinimum, out var slotMaximum)',
    'minimum = Vector2.Min(minimum, slotMinimum)',
    'maximum = Vector2.Max(maximum, slotMaximum)',
    'IsVisibleDescendant(node, addonRoot)',
    'GetAddonByName<AddonPartyList>("_PartyList")',
    'AgentHUD.Instance()',
    'agent->PartyMemberCount is < 1 or > 8',
    'nativeMember.Index >= 8',
    'player!.Address != (nint)nativeMember.Object',
    'player.EntityId != nativeMember.EntityId',
    'CaptureCcRows("PvPMKSPartyList1", friendly: true',
    'CaptureCcRows("PvPMKSPartyList3", friendly: false',
    'for (var slot = 1; slot <= 5; slot++)',
    'PartySlotResolver.Resolve(objectTable, slot)',
    'EnemySlotResolver.Resolve(objectTable, slot)',
    'addon->GetComponentByNodeId((uint)(5 + slot))',
    'row->GetTextNodeById(21)',
    'string.Equals(name->GetText().ToString(), player.Name.TextValue, StringComparison.Ordinal)',
    'node->GetBounds(&bounds)',
    'size.X is > 2f and < 10_000f',
    'size.Y is > 2f and < 10_000f',
    'while (node != null && depth++ < 64)',
    'return depth is > 0 and < 64'
) 'Exact read-only hotbar, party-row, and CC-row resource anchors'
$captureHotbarsMethod = [regex]::Match(
    $resourceAuraAnchor,
    '(?s)private unsafe void CaptureHotbars\(.*?\r?\n    \}\r?\n\r?\n    private static unsafe void AddHotbarAnchor')
$hotbarAnchorMethods = [regex]::Match(
    $resourceAuraAnchor,
    '(?s)private static unsafe void AddHotbarAnchor\(.*?\r?\n    \}\r?\n\r?\n    private unsafe void CapturePartyRows')
if (-not $captureHotbarsMethod.Success -or -not $hotbarAnchorMethods.Success -or
    $captureHotbarsMethod.Value -match '\bContainerNode\b' -or
    $hotbarAnchorMethods.Value -match '\bContainerNode\b' -or
    $hotbarAnchorMethods.Value -match '\b(RootNode->GetBounds|AtkUnitBase\.RootNode->GetBounds)\b') {
    throw 'Self-hotbar auras must use only the visible ActionBarSlotVector OwnerNode union; ContainerNode/root-bounds fallback is forbidden.'
}
$normalizedHotbarAnchorMethods = $hotbarAnchorMethods.Value -replace '\s+', ' '
if ($normalizedHotbarAnchorMethods -notmatch 'var slotCount = actionBar->SlotCount; var first = actionBar->ActionBarSlotVector\.First; var last = actionBar->ActionBarSlotVector\.Last;' -or
    $normalizedHotbarAnchorMethods -notmatch 'for \(var index = 0; index < slotCount; index\+\+\).*?first\[index\]\.ComponentDragDrop.*?AtkComponentBase\.OwnerNode.*?TryGetActionSlotBounds' -or
    $normalizedHotbarAnchorMethods -notmatch 'return found;') {
    throw 'Self-hotbar anchoring must validate the slot vector and union only visible per-slot OwnerNode bounds.'
}
if ($normalizedResourceAuraAnchor -notmatch 'foreach \(var identity in manaStates\.Keys\.Where\(identity => !seen\.Contains\(identity\)\)\.ToArray\(\)\) manaStates\.Remove\(identity\)' -or
    $resourceAuraAnchor -match '->\w+\s*=(?!=)' -or
    $resourceAuraAnchor -match '\b(SetRawValue|ClearAll|FireCallback|SendEvent|SetPosition|SetScale|SetAlpha|SetAdditive|SetMultiply|SetColor|PulseActionBarSlot|UseAction|UseActionLocation)\s*\(') {
    throw 'Resource aura anchoring must discard stale exact identities and remain a strictly read-only native boundary.'
}

Assert-Literals $overlay @(
    'DrawResourceAuras(now)',
    'resourceAuraAnchors.Capture()',
    'resourceAuraAnchors.CaptureSelfHotbarsForPreview()',
    'if (anchors.Count == 0 || ResourceAuraPreviewEnabled) return;',
    'ImGui.GetForegroundDrawList()',
    'ResourceAuraKind.LowHp => LowHealthAuraColor',
    'ResourceAuraKind.LowMp => LowManaAuraColor',
    'ResourceAuraKind.LowHpAndMp => CombinedResourceAuraColor',
    'anchor.Surface == ResourceAuraSurface.SelfHotbar ? 1f : 0.38f',
    'draw.AddRectFilled(',
    'draw.AddRect('
) 'Visual-only native-surface resource aura renderer'
$normalizedOverlay = $overlay -replace '\s+', ' '
$resourceAuraPreviewMethod = [regex]::Match(
    $normalizedOverlay,
    'private void DrawResourceAuraPreview\(\) \{(?<Body>.*?)\} private void DrawResourceAura\(')
if (-not $resourceAuraPreviewMethod.Success -or
    $resourceAuraPreviewMethod.Groups['Body'].Value -match '\bDisplaySize\b|new Vector2\(430f, 58f\)|screen\.Y \* 0\.78f') {
    throw 'Resource-aura preview must use exact current self-hotbar anchors and must not restore the fixed DisplaySize 430-by-58 screen rectangle.'
}

# The v0.30 runtime deliberately retires fixed Combat Frames. Keep this absence
# explicit so legacy compatibility models cannot silently regain a renderer,
# target click/mouseover service, snapshot loop, settings page, or gauge reader.
$retiredCombatFrameRuntimeRelativePaths = @(
    'src/SeitonSense.Plugin/Services/CombatFrameLimitGaugeService.cs',
    'src/SeitonSense.Plugin/Services/CombatFrameLimitGaugeSnapshot.cs',
    'src/SeitonSense.Plugin/Services/CombatFramesSnapshot.cs',
    'src/SeitonSense.Plugin/Services/CombatFramesSnapshotService.cs',
    'src/SeitonSense.Plugin/Services/CombatFramesTargetingService.cs',
    'src/SeitonSense.Plugin/UI/CombatFramesOptions.cs',
    'src/SeitonSense.Plugin/UI/CombatFramesRenderer.cs',
    'src/SeitonSense.Plugin/UI/CombatFramesRenderer.LimitBreaks.cs',
    'src/SeitonSense.Plugin/UI/Settings/SettingsWindow.CombatFrames.cs'
)
foreach ($relativePath in $retiredCombatFrameRuntimeRelativePaths) {
    $retiredPath = Join-Path $resolvedRoot ($relativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    if (Test-Path -LiteralPath $retiredPath) {
        throw "Retired Combat Frames runtime file returned: $relativePath"
    }
}


# Combat LB telemetry reuses the existing sole ActionEffect hook. Catalog,
# captures, metadata, runtime episodes, live-status duration evidence, read-only
# gauge trust, and rendering are all independently fail closed and bounded.
$combatLimitBreakCatalog = Read-RequiredSource $combatLimitBreakCatalogPath 'Combat LB catalog'
$combatLimitBreakEventRules = Read-RequiredSource $combatLimitBreakEventRulesPath 'Combat LB event rules'
$normalizedCombatLimitBreakEventRules = $combatLimitBreakEventRules -replace '\s+', ' '
$combatLimitBreakSelfTests = Read-RequiredSource $combatLimitBreakSelfTestsPath 'Combat LB self-tests'
$combatLimitBreakCaptureBuffer = Read-RequiredSource $combatLimitBreakCaptureBufferPath 'Combat LB capture buffer'
$normalizedCombatLimitBreakCaptureBuffer = $combatLimitBreakCaptureBuffer -replace '\s+', ' '
$combatLimitBreakMetadataGuard = Read-RequiredSource $combatLimitBreakMetadataGuardPath 'Combat LB metadata guard'
$normalizedCombatLimitBreakMetadataGuard = $combatLimitBreakMetadataGuard -replace '\s+', ' '
$combatLimitBreakRuntime = Read-RequiredSource $combatLimitBreakRuntimeServicePath 'Combat LB runtime'
$normalizedCombatLimitBreakRuntime = $combatLimitBreakRuntime -replace '\s+', ' '
$limitBreakNotificationRenderer = Read-RequiredSource $limitBreakNotificationRendererPath 'Combat LB notification renderer'
$normalizedLimitBreakNotificationRenderer = $limitBreakNotificationRenderer -replace '\s+', ' '
$overlayRendererLimitBreaks = Read-RequiredSource $overlayRendererLimitBreaksPath 'Combat LB nameplate renderer'
$normalizedOverlayRendererLimitBreaks = $overlayRendererLimitBreaks -replace '\s+', ' '

$catalogDefinitions = @([regex]::Matches(
    $combatLimitBreakCatalog,
    'Definition\(\s*(?<Job>\d+),\s*"(?<Abbreviation>[^"]+)",\s*"(?<Name>[^"]+)",\s*(?<Icon>[\d_]+),\s*(?<Charge>\d+),\s*CombatLimitBreakPresentationKind\.(?<Presentation>\w+)'))
$actualLimitBreakDefinitions = @($catalogDefinitions | ForEach-Object {
    '{0}|{1}|{2}|{3}|{4}|{5}' -f
        $_.Groups['Job'].Value,
        $_.Groups['Abbreviation'].Value,
        $_.Groups['Name'].Value,
        (($_.Groups['Icon'].Value) -replace '_', ''),
        $_.Groups['Charge'].Value,
        $_.Groups['Presentation'].Value
})
$expectedLimitBreakDefinitions = @(
    '19|PLD|Phalanx|9586|135|Duration',
    '21|WAR|Primal Scream|9592|90|Duration',
    '32|DRK|Eventide|9597|105|Duration',
    '37|GNB|Relentless Rush|9603|60|Duration',
    '24|WHM|Afflatus Purgation|9610|60|Duration',
    '28|SCH|Seraphism|9068|90|Duration',
    '33|AST|Celestial River|9621|105|Duration',
    '40|SGE|Mesotes|9624|120|Duration',
    '20|MNK|Meteodrive|9646|75|Duration',
    '22|DRG|Sky High|9652|90|Duration',
    '30|NIN|Seiton Tenchu|9661|90|Duration',
    '34|SAM|Zantetsuken|9666|120|Instant',
    '39|RPR|Tenebrae Lemurum|9670|60|Duration',
    '41|VPR|World-swallower|9731|90|Duration',
    '23|BRD|Final Fantasia|9629|120|Duration',
    "31|MCH|Marksman's Spite|9636|90|Instant",
    '38|DNC|Contradance|9641|90|Duration',
    '25|BLM|Soul Resonance|9673|60|Duration',
    '27|SMN|Summon Bahamut / Phoenix|9681|90|Duration',
    '35|RDM|Southern Cross|9692|90|Instant',
    '42|PCT|Advent of Chocobastion|9757|105|Duration'
)
if (($actualLimitBreakDefinitions -join "`n") -cne ($expectedLimitBreakDefinitions -join "`n")) {
    throw 'Combat LB catalog must retain the exact current 21-job names, icons, charge metadata, and presentation kinds.'
}
$actualLimitBreakActivationIds = @([regex]::Matches(
    $combatLimitBreakCatalog,
    '\bActivation(?:Damage)?\(\s*(?<Id>[\d_]+)') | ForEach-Object {
        [uint32](($_.Groups['Id'].Value) -replace '_', '')
    })
$expectedLimitBreakActivationIds = [uint32[]]@(
    29069, 29083, 29097, 29130, 29230, 41502, 29255, 29266, 29485, 29497, 29515,
    29537, 29553, 39190, 29401, 29415, 29432, 29662, 29673, 29678, 41498, 39215)
$actualLimitBreakStatusIds = @([regex]::Matches(
    $combatLimitBreakCatalog,
    '\b(?:Caster|Target)Status\(\s*(?<Id>[\d_]+)') | ForEach-Object {
        [uint32](($_.Groups['Id'].Value) -replace '_', '')
    })
$expectedLimitBreakStatusIds = [uint32[]]@(
    1302, 3250, 1303, 3185, 4287, 3833, 4286, 3901, 3039, 3033, 4290, 3837,
    3052, 2037, 4327, 3094, 3105, 4332, 3893, 3118, 3174, 3180, 3181, 3191,
    3192, 2863, 2593, 4094, 3144, 4312, 3024, 3222, 4317, 3228, 3229, 4116, 4118)
if (($actualLimitBreakActivationIds -join ',') -ne ($expectedLimitBreakActivationIds -join ',') -or
    ($actualLimitBreakStatusIds -join ',') -ne ($expectedLimitBreakStatusIds -join ',') -or
    $combatLimitBreakCatalog -notmatch 'public const long InstantFlashMilliseconds = 1_800;' -or
    $combatLimitBreakCatalog -notmatch 'Activation\(29_678, 9_683\)' -or
    $combatLimitBreakCatalog -notmatch 'CombatLimitBreakDamageAttribution\.PetOwnerRequired' -or
    $combatLimitBreakCatalog -notmatch 'CombatLimitBreakDamageAttribution\.PeriodicOwnerRequired') {
    throw 'Combat LB catalog must retain exactly 22 activation variants, 37 status bindings, 1.8-second instant flashes, the Phoenix icon override, and explicit pet/periodic exclusions.'
}
$expectedDirectCasterDamageActionIds = [uint32[]]@(
    29071, 29072, 29073, 41433, 29097, 41437, 29557, 29131, 29469, 29230,
    41500, 41508, 29485, 29498, 29499, 29515, 29516, 29537, 39190, 39173,
    41467, 29415, 41480, 41481, 41484, 41485, 41498, 39216, 39217)
$directCasterCatalogIds = @([regex]::Matches(
    $combatLimitBreakCatalog,
    '\b(?:ActivationDamage|FollowUpDamage)\(\s*(?<Id>[\d_]+)') | ForEach-Object {
        [uint32](($_.Groups['Id'].Value) -replace '_', '')
    })
if (($directCasterCatalogIds -join ',') -ne ($expectedDirectCasterDamageActionIds -join ',')) {
    throw 'Combat LB direct-caster attribution must remain the exact reviewed 29-action set; pet and periodic rows may not enter it.'
}
Assert-Literals $combatLimitBreakCatalog @(
    'action.DamageAttribution == CombatLimitBreakDamageAttribution.DirectCaster',
    'CombatLimitBreakActionRole.FollowUp | CombatLimitBreakActionRole.Damage,',
    'new(actionId, CombatLimitBreakActionRole.FollowUp, attribution)',
    'CombatLimitBreakStatusCarrier.Target, true, phase'
) 'Exact activation/direct-caster/pet/periodic/status-carrier catalog roles'

$damageActionDictionary = [regex]::Match(
    $combatLimitBreakMetadataGuard,
    '(?s)DamageActions\s*=\s*new Dictionary<uint, ExpectedDamageAction>\s*\{(?<Body>.*?)\n\s*\};')
$metadataDamageIds = @([regex]::Matches(
    $damageActionDictionary.Groups['Body'].Value,
    '\[(?<Id>[\d_]+)\]\s*=') | ForEach-Object {
        [uint32](($_.Groups['Id'].Value) -replace '_', '')
    })
$activationNameDictionary = [regex]::Match(
    $combatLimitBreakMetadataGuard,
    '(?s)ActivationNames\s*=\s*new Dictionary<uint, string>\s*\{(?<Body>.*?)\n\s*\};')
$metadataActivationIds = @([regex]::Matches(
    $activationNameDictionary.Groups['Body'].Value,
    '\[(?<Id>[\d_]+)\]\s*=') | ForEach-Object {
        [uint32](($_.Groups['Id'].Value) -replace '_', '')
    })
if (-not $damageActionDictionary.Success -or -not $activationNameDictionary.Success -or
    ($metadataDamageIds -join ',') -ne ($expectedDirectCasterDamageActionIds -join ',') -or
    ($metadataActivationIds -join ',') -ne ($expectedLimitBreakActivationIds -join ',')) {
    throw 'Combat LB metadata must independently enumerate all 22 activations and all 29 exact direct-caster damage actions.'
}
Assert-Literals $combatLimitBreakMetadataGuard @(
    'int VerifiedDamageActions,',
    'int ExpectedDamageActions,',
    'CombatLimitBreakCatalog.IsDirectlyAttributableDamage(action)',
    'ValidateDamageAction(definition, binding, damageAction)',
    'verifiedDamageActions == expectedDamageActions',
    'verifiedStatuses == expectedStatuses',
    'ActivationNames.TryGetValue(binding.ActionId, out var expectedName)',
    'action.Icon == CombatLimitBreakCatalog.ResolveIconId(definition, binding)',
    'DamageActions.TryGetValue(binding.ActionId, out var expected)',
    'action.IsPlayerAction == expected.IsPlayerAction',
    'action.ClassJob.RowId == definition.JobId',
    'action.ClassJobCategory.RowId == expected.ClassJobCategoryId',
    'action.ActionCategory.RowId == expected.ActionCategoryId',
    'status.RowId == binding.StatusId',
    'status.Icon != 0',
    '!status.IsPermanent',
    'status.StatusCategory is 1 or 2'
) 'Exact activation, 29 direct-damage-action, and 37 status metadata verification'
if ($normalizedCombatLimitBreakMetadataGuard -notmatch 'var verified = verifiedActions == expectedActions && verifiedDamageActions == expectedDamageActions && verifiedStatuses == expectedStatuses && firstInvalidAction == 0 && firstInvalidStatus == 0;' -or
    $combatLimitBreakMetadataGuard -match '\b(?:UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|HookFromAddress)\b') {
    throw 'Combat LB metadata.Verified must require every activation, damage action, and status while remaining read-only.'
}

Assert-Literals $combatLimitBreakEventRules @(
    'public const byte DamageEffectType = 3;',
    'public const byte BlockedDamageEffectType = 5;',
    'public const byte ParriedDamageEffectType = 6;',
    'public const byte LargeValueFlag = 0x40;',
    'public const byte AppliedToSourceFlag = 0x80;',
    'public const long MaximumTrackedDurationMilliseconds = 3_600_000;',
    'observation.RemainingSeconds',
    'CombatLimitBreakStatusCarrier.Caster',
    'CombatLimitBreakStatusCarrier.Target',
    'observation.SourceEntityId == casterEntityId'
) 'Fail-closed direct-damage decoder and exact live status duration evidence'
if ($normalizedCombatLimitBreakEventRules -notmatch 'effectType is not \(DamageEffectType or BlockedDamageEffectType or ParriedDamageEffectType\).*?\(param4 & AppliedToSourceFlag\) != 0.*?\(param4 & LargeValueFlag\) == 0 && param3 != 0' -or
    $normalizedCombatLimitBreakEventRules -notmatch '!float\.IsFinite\(observation\.RemainingSeconds\) \|\| observation\.RemainingSeconds <= 0f' -or
    $combatLimitBreakEventRules -match '\b(?:Environment\.TickCount64|DateTime|Stopwatch|GaugeChargeSeconds|UseAction|ITargetManager|TargetManager)\b') {
    throw 'Combat LB Core must decode only exact direct damage and derive duration solely from finite positive live RemainingTime on the exact carrier/source.'
}
$combatLimitBreakTestMethods = @(
    'CatalogIsCompleteCurrentAndUnique',
    'DamageDecoderIsExactAndFailClosed',
    'DurationEvidenceRequiresExactCarrierAndSource'
)
foreach ($method in $combatLimitBreakTestMethods) {
    Assert-Literals $combatLimitBreakSelfTests @("internal static void $method()") "Combat LB self-test $method"
    Assert-Literals $coreSelfTestProgramForGuardian @("CombatLimitBreakSelfTests.$method") "Combat LB test registration $method"
}
Assert-Literals $combatLimitBreakSelfTests @(
    'uint[] expectedDirectCasterDamageActions =',
    'expectedDirectCasterDamageActions.Order().SequenceEqual(directCasterDamageActions)',
    'PetOwnerRequired',
    'source-applied damage is rejected'
) 'Combat LB exact 29-action and attribution regressions'
if ([regex]::Matches($combatLimitBreakSelfTests, '\binternal static void\s+\w+\s*\(').Count -ne 3 -or
    [regex]::Matches($coreSelfTestProgramForGuardian, '\bCombatLimitBreakSelfTests\.\w+').Count -ne 3) {
    throw 'All three Combat LB catalog, damage-decoder, and live-duration tests must remain registered exactly once.'
}

Assert-Literals $combatLimitBreakCaptureBuffer @(
    'private const int EffectSlotsPerTarget = 8;',
    'private const int MaximumTargetsPerAction = 32;',
    'private const int MaximumQueuedActivations = 64;',
    'private const int MaximumQueuedDamageEvents = 256;',
    'private int captureMode;',
    'internal bool DamageEnabled => Volatile.Read(ref captureMode) == 2;',
    'internal void SetEnabled(bool value, bool includeDamage = false)',
    'var next = value ? includeDamage ? 2 : 1 : 0;',
    'Interlocked.Exchange(ref captureMode, next)',
    'Interlocked.Increment(ref featureGeneration)',
    'CombatLimitBreakCatalog.IsActivation(action)',
    '!DamageEnabled ||',
    '!CombatLimitBreakCatalog.IsDirectlyAttributableDamage(action)',
    'CombatLimitBreakEventRules.TryDecodeDirectDamage(',
    'activation.FeatureGeneration == FeatureGeneration',
    'damage.FeatureGeneration == FeatureGeneration'
) 'Bounded value-only atomic activation/damage capture modes'
if ($normalizedCombatLimitBreakCaptureBuffer -notmatch 'if \(CombatLimitBreakCatalog\.IsActivation\(action\)\).*?Enqueue\(new CombatLimitBreakActivationCapture' -or
    $normalizedCombatLimitBreakCaptureBuffer -notmatch 'if \(!DamageEnabled \|\| !CombatLimitBreakCatalog\.IsDirectlyAttributableDamage\(action\).*?\) \{ return; \}' -or
    [regex]::Matches($combatLimitBreakCaptureBuffer, '\bCombatLimitBreakEventRules\.TryDecodeDirectDamage\s*\(').Count -ne 1 -or
    [regex]::Matches($combatLimitBreakCaptureBuffer, '\bEnqueue\s*\(\s*new CombatLimitBreakDamageCapture\s*\(').Count -ne 1 -or
    $combatLimitBreakCaptureBuffer -match '\b(?:Hook<|HookFromAddress|IGameInteropProvider|IPlayerCharacter|IGameObject|Name|UseAction|ITargetManager|TargetManager)\b') {
    throw 'Combat LB capture buffer must install no hook, retain no wrappers/names, always bound activation capture, and keep exactly one damage decoder/enqueue path behind packed mode 2.'
}
if ([regex]::Matches($mchCapture, '\bHookFromAddress\s*\(').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bcombatLimitBreakCaptureBuffer\.Capture\s*\(').Count -ne 1 -or
    [regex]::Matches($mchCapture, '\bactionEffectHook!\.OriginalDisposeSafe\s*\(').Count -ne 1 -or
    $normalizedMchCapture -notmatch 'try \{ if \(Volatile\.Read\(ref captureBlocked\) == 0\).*?combatLimitBreakCaptureBuffer\.Capture\( casterEntityId, header, effects, targetEntityIds\); \}.*?finally \{ actionEffectHook!\.OriginalDisposeSafe') {
    throw 'The existing MCH/pressure ActionEffect detour must remain the sole hook, forward once to the LB sidecar, and invoke Original exactly once in finally.'
}

Assert-Literals $combatLimitBreakRuntime @(
    'private const long MaximumCaptureAgeMilliseconds = 5_000;',
    'private const long FutureCaptureToleranceMilliseconds = 250;',
    'private const long ConfirmedStatusLossGraceMilliseconds = 150;',
    'private const long AllyDamageEventLifetimeMilliseconds = 3_000;',
    'private const int MaximumDisplayNameCharacters = 40;',
    'private const int MaximumActivationKeys = 256;',
    'private const int MaximumDamageKeys = 1_024;',
    'private const int MaximumVisibleAllyDamageEvents = 32;',
    'if (!metadata.Verified ||',
    'var damageFeedEnabled = damageFeedEnabledProvider();',
    'captureBuffer.SetEnabled(true, damageFeedEnabled);',
    'allyDamageEvents.Clear();',
    'damageKeys.Clear();',
    'damageKeyOrder.Clear();',
    'RefreshEpisodes(roster, nowMilliseconds);',
    'DrainActivations(roster, nowMilliseconds);',
    'if (damageFeedEnabled)',
    'DrainDamageEvents(roster, nowMilliseconds);',
    'status.RemainingTime',
    'evidence.RemainingMilliseconds',
    'ConfirmedStatusLossGraceMilliseconds',
    'CombatLimitBreakCatalog.InstantFlashMilliseconds',
    'CombatLimitBreakCatalog.IsDirectlyAttributableDamage(action)',
    'caster.Actor.Side != CombatLimitBreakRosterSide.Ally',
    'target.Actor.Side != CombatLimitBreakRosterSide.Enemy',
    'episodes.TryGetValue(caster.Actor.Identity, out var episode)',
    'internal bool TryResolveCurrentDamageDisplayNames(',
    'PartySlotResolver.Resolve(objectTable, damageEvent.CasterPartySlot)',
    'EnemySlotResolver.Resolve(objectTable, damageEvent.TargetEnemySlot)',
    '.Take(MaximumDisplayNameCharacters)'
) 'Exact-metadata LB runtime, activation-only privacy mode, live duration, and direct ally damage'
if ($normalizedCombatLimitBreakRuntime -notmatch 'if \(!metadata\.Verified \|\| !enabledProvider\(\).*?!TryBuildExactRoster\(out var roster\)\) \{ Deactivate\(\); return; \} var damageFeedEnabled = damageFeedEnabledProvider\(\);' -or
    $normalizedCombatLimitBreakRuntime -notmatch 'else captureBuffer\.SetEnabled\(true, damageFeedEnabled\); if \(damageFeedEnabled\) RemoveExpiredDamageEvents\(nowMilliseconds\); else \{ allyDamageEvents\.Clear\(\); damageKeys\.Clear\(\); damageKeyOrder\.Clear\(\); \} RefreshEpisodes' -or
    $normalizedCombatLimitBreakRuntime -notmatch 'if \(damageFeedEnabled\) DrainDamageEvents\(roster, nowMilliseconds\);' -or
    [regex]::Matches($combatLimitBreakRuntime, '\bcaptureBuffer\.SetEnabled\s*\(').Count -ne 5 -or
    [regex]::Matches($combatLimitBreakRuntime, '\bcaptureBuffer\.SetEnabled\s*\(\s*true\s*,\s*damageFeedEnabled\s*\)').Count -ne 2 -or
    [regex]::Matches($combatLimitBreakRuntime, '\bcaptureBuffer\.SetEnabled\s*\(\s*false\s*\)').Count -ne 3 -or
    $normalizedCombatLimitBreakRuntime -notmatch 'episode\.ExpiresAtMilliseconds = SaturatingAdd\( nowMilliseconds, evidence\.RemainingMilliseconds\);.*?episode\.MissingStatusSinceMilliseconds = -1;.*?nowMilliseconds - episode\.MissingStatusSinceMilliseconds >= ConfirmedStatusLossGraceMilliseconds\) episodes\.Remove' -or
    $normalizedCombatLimitBreakRuntime -notmatch 'caster\.Actor\.Side != CombatLimitBreakRosterSide\.Ally \|\| target\.Actor\.Side != CombatLimitBreakRosterSide\.Enemy.*?!episodes\.TryGetValue.*?!ActionBelongsToEpisode' -or
    $combatLimitBreakRuntime -match '\b(?:GaugeChargeSeconds|EstimatedRecharge|ElapsedCharge|UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget)\b') {
    throw 'Combat LB runtime must require global metadata.Verified, keep activation episodes when damage display disables, clear all damage presentation/dedupe state, never estimate duration/gauge, and accept only live exact ally-caster-to-enemy direct damage within its activation episode.'
}


$combatLimitBreakNameplateRules = Read-RequiredSource $combatLimitBreakNameplateRulesPath 'Combat LB nameplate rules'
$combatLimitBreakNameplateSelfTests = Read-RequiredSource $combatLimitBreakNameplateSelfTestsPath 'Combat LB nameplate self-tests'
$combatLimitBreakNotificationRules = Read-RequiredSource $combatLimitBreakNotificationRulesPath 'Combat LB notification rules'
$combatLimitBreakNotificationSelfTests = Read-RequiredSource $combatLimitBreakNotificationSelfTestsPath 'Combat LB notification self-tests'
Assert-Literals $combatLimitBreakNameplateRules @(
    'MaximumSnapshotAgeMilliseconds = 500',
    'MaximumAnchorAgeMilliseconds = 250',
    '!observation.IsEnemy',
    'var showCountdown = observation.Presentation == CombatLimitBreakPresentationKind.Duration &&',
    'observation.DurationConfirmed;',
    'CombatLimitBreakCatalog.InstantFlashMilliseconds',
    'TryBuildVerticalStack('
) 'Fresh exact enemy LB nameplate admission and bounded non-overlap'
Assert-Literals ($overlayRendererLimitBreaks + $limitBreakNotificationRenderer) @(
    'CombatLimitBreakNameplateRules.TryBuildDisplayPlan(',
    'CombatLimitBreakNameplateRules.TryBuildVerticalStack(',
    'CombatLimitBreakNotificationRules.TryBuildSelfPlan(',
    'CombatLimitBreakNotificationRules.TryBuildDamagePlan(',
    'CombatLimitBreakNotificationRules.MaximumVisibleDamageCards',
    'LB ACTIVATED!',
    'runtime.TryResolveCurrentDamageDisplayNames('
) 'Standalone enemy-nameplate and self/ally LB renderers'
if (($overlayRendererLimitBreaks + $limitBreakNotificationRenderer) -match
    '\b(?:UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget)\b|\.(?:Target|FocusTarget|SoftTarget|MouseOverTarget|MouseOverNameplateTarget|GPoseTarget)\s*=(?!=|>)') {
    throw 'Replacement LB renderers must remain non-interactive and target-mutation free.'
}
$replacementLbTests = @(
    @($combatLimitBreakNameplateSelfTests, 'CombatLimitBreakNameplateSelfTests', @(
        'DisplayRequiresFreshExactEnemyIdentity',
        'CountdownRequiresConfirmedDurationAndFlashIsBounded',
        'VerticalStackIsDeterministicAndNeverOverlaps')),
    @($combatLimitBreakNotificationSelfTests, 'CombatLimitBreakNotificationSelfTests', @(
        'SelfBannerRequiresExactFreshEvidence',
        'AllyDamageCardsRequireExactBoundedEvents',
        'NotificationLayoutStaysInsideSafeScreenLanes'))
)
foreach ($testGroup in $replacementLbTests) {
    foreach ($method in $testGroup[2]) {
        Assert-Literals $testGroup[0] @("internal static void $method()") "$($testGroup[1]) self-test $method"
        Assert-Literals $coreSelfTestProgramForGuardian @("$($testGroup[1]).$method") "$($testGroup[1]) registration $method"
    }
}
$normalizedPluginSource = $pluginSource -replace '\s+', ' '


$settingsPageContracts = [ordered]@{
    Start = @('Start', 'DrawStartPage')
    Alerts = @('Alerts', 'DrawAlertsPage')
    HudAndNameplates = @('HUD & Nameplates', 'DrawHudAndNameplatesPage')
    ActionHelpers = @('Action Helpers', 'DrawActionHelpersPage')
    JobTools = @('Job Tools', 'DrawJobToolsPage')
    MacroHelpers = @('Macro Helpers', 'DrawMacroHelpersPage')
    Targets = @('Targets', 'DrawTargetsPage')
    Diagnostics = @('Diagnostics', 'DrawDiagnosticsPage')
}
Assert-Literals $settingsWindow @(
    'internal sealed partial class SettingsWindow',
    'private SettingsPage selectedPage = SettingsPage.Start;',
    '##SeitonSenseSettingsSidebar',
    '##SeitonSenseSettingsContent{selectedPage}',
    'private void DrawSidebar()',
    'private void DrawPageChoice(SettingsPage page, string label)',
    'private enum SettingsPage'
) 'Split SettingsWindow shell and sidebar contract'
if ([regex]::Matches($settingsWindow, 'internal sealed partial class SettingsWindow').Count -ne $settingsSourceFiles.Count) {
    throw 'Every SettingsWindow source in the reviewed split must remain one partial of the same sealed window.'
}

$settingsPageEnum = [regex]::Match(
    $settingsWindow,
    '(?s)private enum SettingsPage\s*\{(?<Body>.*?)\}')
if (-not $settingsPageEnum.Success) {
    throw 'The Settings sidebar page enum is missing.'
}

$actualSettingsPages = @([regex]::Matches(
        $settingsPageEnum.Groups['Body'].Value,
        '(?m)^\s*(?<Name>[A-Za-z_]\w*)\s*,\s*$') | ForEach-Object { $_.Groups['Name'].Value })
$settingsPageDifference = @(
    Compare-Object -ReferenceObject @($settingsPageContracts.Keys) -DifferenceObject $actualSettingsPages
)
if ($settingsPageDifference.Count -ne 0) {
    $pageDetails = $settingsPageDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
    throw "Settings sidebar page set drifted: $($pageDetails -join ', ')"
}

$sidebarOrderPatterns = @()
foreach ($entry in $settingsPageContracts.GetEnumerator()) {
    $pageName = [string]$entry.Key
    $pageLabel = [string]$entry.Value[0]
    $pageMethod = [string]$entry.Value[1]
    $choicePattern = 'DrawPageChoice\(SettingsPage\.' + [regex]::Escape($pageName) +
        ',\s*"' + [regex]::Escape($pageLabel) + '"\)'
    $switchPattern = 'SettingsPage\.' + [regex]::Escape($pageName) +
        '\s*=>\s*' + [regex]::Escape($pageMethod) + '\(\)'
    $methodPattern = 'private\s+bool\s+' + [regex]::Escape($pageMethod) + '\(\)'
    if ([regex]::Matches($settingsWindow, $choicePattern).Count -ne 1 -or
        [regex]::Matches($settingsWindow, $switchPattern).Count -ne 1 -or
        [regex]::Matches($settingsWindow, $methodPattern).Count -ne 1) {
        throw "Settings page '$pageLabel' must have exactly one sidebar choice, switch route, and page renderer."
    }

    $sidebarOrderPatterns += $choicePattern
}
if ($normalizedSettingsWindow -notmatch ($sidebarOrderPatterns -join '.*?')) {
    throw 'Settings sidebar order must remain Start, Alerts, HUD & Nameplates, Action Helpers, Job Tools, Macro Helpers, Targets, Diagnostics.'
}
if ($settingsWindow -match 'ImGui\.BeginTabItem|DrawJobsTab') {
    throw 'The sidebar Settings structure must not silently fall back to the obsolete monolithic tab/DrawJobsTab layout.'
}


Assert-Literals $settingsWindow @(
    'Play local MP threshold sounds',
    '4,000 MP warning sound',
    '2,000 MP critical sound',
    'A direct drop through both thresholds plays only the urgent 2,000-MP cue.',
    'Show my LB ACTIVATED banner',
    'Show ally LB damage cards on the left',
    'Show active enemy LB icons above exact native nameplates',
    'unknown duration is never guessed.',
    'Enable one-shot /smarttab, /nearassist, /nearhelp, and /farhelp targeting',
    'Smart Target macro — harmful action',
    '/smarttab arms one 750 ms token',
    'invalidates that carrier and leaves the following <t> line as the only fallback.',
    'Use /autoseiton (or click the movable action-bar tile) to switch this availability ON/OFF.',
    'ON still requires',
    'a currently held gameplay key; it never creates no-input automatic actions.',
    'Purify > NIN Seiton > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi >'
) 'v0.30 replacement LB, local MP, Smart Target, Auto-Seiton, and priority Settings copy'
if ($settingsWindow -match 'DrawCombatFramesPage|SettingsPage\.CombatFrames|Show fixed Combat Frames') {
    throw 'Retired Combat Frames must not retain a Settings page or runtime toggle.'
}

$settingsConfigurationPath = Join-Path $sourceRoot 'SeitonSense.Plugin\Models\PluginConfiguration.cs'
$settingsConfiguration = Read-RequiredSource $settingsConfigurationPath 'Settings binding configuration'
$configurationPropertyMatches = [regex]::Matches(
    $settingsConfiguration,
    '(?m)^\s*public\s+[A-Za-z_][\w<>, ?]*\s+(?<Name>[A-Za-z_]\w*)\s*\{\s*get;\s*set;\s*\}')
$configurationPropertyNames = @($configurationPropertyMatches | ForEach-Object { $_.Groups['Name'].Value })
$settingsBindingExemptions = @(
    'Version',
    'ExperimentalPurifyBufferMilliseconds',
    'ExperimentalMiracleInterceptOnHeldKey',
    'MiracleInterceptAfterPurifiedStun',
    'EnableSageKardiaOnHeldKey',
    'EnableNinjaSeitonOnFreshGameplayKey',
    'PreGuardOnLowHpPressure',
    'LastSeenReleaseNotesVersion',
    'ShowCombatFrames',
    'CombatFramesEnableInteraction',
    'CombatFramesShowLimitBreaks',
    'CombatFramesShowNames',
    'CombatFramesShowExactValues',
    'CombatFramesShowStatuses',
    'CombatFramesShowPressure',
    'CombatFramesEnemyScreenX',
    'CombatFramesEnemyScreenY',
    'CombatFramesSelfScreenX',
    'CombatFramesSelfScreenY',
    'CombatFramesScale',
    'CombatFramesBackgroundOpacity',
    'CcBrakeJobs',
    'CcBrakeActions'
)
foreach ($exemption in $settingsBindingExemptions) {
    if ($configurationPropertyNames -notcontains $exemption) {
        throw "Reviewed Settings binding exemption '$exemption' no longer names a persisted configuration property."
    }
}

$settingsBindingFailures = @()
foreach ($propertyName in $configurationPropertyNames) {
    if ($settingsBindingExemptions -contains $propertyName) { continue }
    $escapedPropertyName = [regex]::Escape($propertyName)
    $referenceCount = [regex]::Matches(
        $settingsWindow,
        '\bconfiguration\.' + $escapedPropertyName + '\b').Count
    $writeCount = [regex]::Matches(
        $settingsWindow,
        '\bconfiguration\.' + $escapedPropertyName + '\s*=').Count
    $expectedWriteCount = 1
    if ($referenceCount -lt 2 -or $writeCount -ne $expectedWriteCount) {
        $settingsBindingFailures += "$propertyName (references=$referenceCount, writes=$writeCount)"
    }
}
if ($settingsBindingFailures.Count -ne 0) {
    throw "Persisted Settings bindings drifted or became unreachable: $($settingsBindingFailures -join ', ')"
}
Assert-Literals $settingsWindow @(
    'NATIVE FOCUS TARGET SETTER (OPT-IN)',
    'Set an empty Focus Target to an exact enemy at 2,000 MP or lower',
    'configuration.EnableAutoLowMpFocusTarget',
    'Default off and exact Crystalline Conflict only',
    'complete unique native S1-S5 view',
    '150 ms of trusted MP at 2,000 or lower',
    'native 20-yalm range/line-of-sight result',
    'clears only after 150 ms at 2,300 MP or higher',
    'fill only an empty native Focus Target and never clears, replaces, restores, or retries',
    'occupied or manually changed Focus always wins',
    'latches the manual override',
    'feeds FFXIV''s Focus Target HUD and <f>',
    'independent of the party-visible',
    'Attack1 sign and never changes your hard or soft target',
    'no atomic compare-and-set API',
    'requires a live current-patch CC',
    'A/B test.',
    'DARK KNIGHT SHADOWBRINGER MACRO (OPT-IN)',
    'Enable the exact two-line /seitonbringer weave helper',
    'configuration.EnableDarkKnightShadowbringerMacro',
    'Enable Wolves'' Den testing for Seiton, native-nameplate cues, and enabled /seitonbringer',
    'configuration.EnableWolvesDenTesting',
    '/seitonbringer also requires its separate Macro Helpers opt-in and',
    'accepts only your exact current hard-target Wolves'' Den striking dummy',
    'it never uses synthetic S1,',
    '<e1>, or the duel opponent',
    '/pvpac \"Souleater Combo\" <t>',
    'enable both Macro Queue and Turbo for this macro',
    'Default off and PvP Dark Knight only. Exact Crystalline Conflict is supported directly',
    'Wolves'' Den additionally requires the existing Start-page test option and accepts only your exact current',
    'hard-target striking dummy. Frontline and Rival Wings remain blocked',
    'at most one Shadowbringer attempt in the inclusive 0.60-0.80',
    '0.50 seconds or less never triggers',
    'In CC, the exact current <t> must remain one canonical S1-S5 enemy',
    'same verified native striking-dummy hard target; synthetic S1, <e1>, duel-opponent resolution',
    'players, and other targets are never fallbacks',
    'native 5-yalm/10-yalm range and line-of-sight checks',
    'more than 12,000 HP or the',
    'exact Dark Arts status/action state',
    'one-attempt token is spent before the final native Shadowbringer request',
    'never changes a target, chooses an alternate action or enemy, replays the macro, or retries',
    'ACCEPTED is local dispatch feedback only',
    'successful Den dummy test does not prove live CC behavior'
) 'Schema-30 Survival, held NIN, cast cancellation, Combat Frames interaction/LB, Auto Focus, DRK Hiebsprung, post-Guard, and dual-opt-in exact Den-dummy DRK Settings bindings and safety copy'

$settingsConfigurationMethodBindings = @(
    'ApplyCurrentTargetHighlightPreset',
    'ApplyFocusGlowPreset',
    'IsCcBrakeActionEnabled',
    'IsCcBrakeJobEnabled',
    'ResetToDefaults',
    'Save',
    'SetCcBrakeActionEnabled',
    'SetCcBrakeJobEnabled'
)
foreach ($methodName in $settingsConfigurationMethodBindings) {
    $methodCallPattern = '\bconfiguration\.' + [regex]::Escape($methodName) + '\s*\('
    if ([regex]::Matches($settingsWindow, $methodCallPattern).Count -ne 1) {
        throw "Settings must retain exactly one reviewed configuration method binding for $methodName."
    }
}
foreach ($legacyPropertyName in @(
        'Version',
        'ExperimentalPurifyBufferMilliseconds',
        'ExperimentalMiracleInterceptOnHeldKey',
        'MiracleInterceptAfterPurifiedStun',
        'EnableSageKardiaOnHeldKey',
        'PreGuardOnLowHpPressure')) {
    if ($settingsWindow -match ('\bconfiguration\.' + [regex]::Escape($legacyPropertyName) + '\b')) {
        throw "Legacy/non-UI configuration property $legacyPropertyName must not silently reappear as a Settings binding."
    }
}
if ($settingsWindow -match '\bconfiguration\.CcBrake(?:Jobs|Actions)\b') {
    throw 'CC-brake Settings must remain behind the reviewed Is/Set methods rather than binding mutable dictionaries directly.'
}

$rangeRules = Read-RequiredSource (Join-Path $coreRoot 'SeitonRangeRules.cs') 'Seiton range rules'
Assert-Literals $rangeRules @('Ready = 0', 'NotFacingTarget = 565', 'HasNativeRangeAndLineOfSight') 'Seiton range allowlist'
if ($readiness -notmatch [regex]::Escape('SeitonRangeRules.HasNativeRangeAndLineOfSight')) {
    throw 'Seiton readiness probe must use the strict native range and line-of-sight allowlist.'
}

$metadataPath = Join-Path $pluginServicesRoot 'SeitonMetadataGuard.cs'
$constantsPath = Join-Path $pluginServicesRoot 'EnemyCombatConstants.cs'
$metadata = Read-RequiredSource $metadataPath 'Metadata guard'
$combatConstants = Read-RequiredSource $constantsPath 'Combat constants'
Assert-Literals $metadata @(
    'SeitonReadinessProbe.BaseActionId',
    'SeitonReadinessProbe.FollowUpActionId',
    'SeitonReadinessProbe.UnsealedStatusId',
    'NinjaSeitonProtectionStatusCatalog.CoveredLegacyStatusId',
    'NinjaSeitonProtectionStatusCatalog.CoveredStatusId',
    'NinjaSeitonProtectionStatusCatalog.CoveredPvpStatusId',
    'NinjaSeitonProtectionStatusCatalog.CoveredPvpAlternateStatusId',
    'NinjaSeitonProtectionStatusCatalog.HallowedGroundStatusId',
    'NinjaSeitonProtectionStatusCatalog.UndeadRedemptionStatusId',
    'ValidateSeitonProtectionStatus(',
    'NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(statusId)',
    'Seiton Tenchu',
    'Unsealed Seiton Tenchu',
    'RequiresLineOfSight',
    'EnemyCombatConstants.GuardActionId',
    'EnemyCombatConstants.RecuperateActionId',
    'EnemyCombatConstants.RecuperateMpCost',
    'EnemyCombatConstants.WildfireActionId',
    'EnemyCombatConstants.WildfireStatusId',
    'EnemyCombatConstants.DeathWarrantActionId',
    'EnemyCombatConstants.DeathWarrantStatusId',
    'EnemyCombatConstants.MarksmanSpiteActionId',
    'EnemyCombatConstants.MarksmanSpiteTimelineId',
    'EnemyCombatConstants.PvPStunStatusId',
    'EnemyCombatConstants.PvPHeavyStatusId',
    'EnemyCombatConstants.PvPBindStatusId',
    'EnemyCombatConstants.PvPSilenceStatusId',
    'EnemyCombatConstants.DeepFreezeStatusId',
    'EnemyCombatConstants.MiracleOfNatureStatusId',
    'EnemyCombatConstants.MiracleOfNatureActionId',
    'EnemyCombatConstants.GuardianActionId',
    'EnemyCombatConstants.ScholarCriticalStrategyActionId',
    'EnemyCombatConstants.ScholarCriticalStrategyIconId',
    'EnemyCombatConstants.ScholarJobId',
    'EnemyCombatConstants.ScholarCriticalStrategySheetRange',
    'EnemyCombatConstants.ScholarCriticalStrategyRecast100ms',
    'EnemyCombatConstants.SilentNocturneActionId',
    'EnemyCombatConstants.ContradanceActionId',
    'EnemyCombatConstants.SeducedStatusId',
    'EnemyCombatConstants.ZantetsukenActionId',
    'EnemyCombatConstants.FuriousBacklashActionId',
    'EnemyCombatConstants.HardenedScalesStatusId',
    'EnemyCombatConstants.PurifyActionId',
    'EnemyCombatConstants.ResilienceStatusId',
    'ValidateFeature("Wildfire"',
    'ValidateFeature("Death Warrant"',
    'ValidateFeature("Marksman''s Spite"',
    'ValidateFeature("Purify"',
    'ValidateFeature("Miracle of Nature action"',
    'ValidateFeature("Guardian"',
    'ValidateFeature("Scholar Critical Strategy"',
    'ValidateFeature("Silent Nocturne"',
    'ValidateFeature("Contradance"',
    'ValidateFeature("Zantetsuken"',
    'ValidateFeature("Furious Backlash"',
    'MiracleOfNatureActionVerified',
    'GuardianVerified',
    'ScholarCriticalStrategyVerified',
    'SilentNocturneVerified',
    'ContradanceVerified',
    'ZantetsukenVerified',
    'FuriousBacklashVerified',
    'Forcibly transforms target',
    'preventing them from using actions other than Purify',
    'nullifies status afflictions that can be removed by Purify',
    'Increases target''s damage taken by 10%',
    'Halves the defensive bonus of Guard instead when targeting enemies under its effect.'
) 'Metadata guard'

$normalizedMetadata = $metadata -replace '\s+', ' '
if ($normalizedMetadata -notmatch 'var scholarCriticalStrategyVerified = ValidateFeature\("Scholar Critical Strategy", log, \(\) => \{ var actions = dataManager\.GetExcelSheet<ActionSheet>\(ClientLanguage\.English\); var descriptions = dataManager\.GetExcelSheet<ActionTransient>\(ClientLanguage\.English\); if \(!actions\.TryGetRow\(EnemyCombatConstants\.ScholarCriticalStrategyActionId, out var action\) \|\| !descriptions\.TryGetRow\(EnemyCombatConstants\.ScholarCriticalStrategyActionId, out var transient\)\).*?return action\.Name\.ToString\(\) == "Chain Stratagem" && action\.Icon == EnemyCombatConstants\.ScholarCriticalStrategyIconId && action\.IsPvP && action\.IsPlayerAction && action\.ClassJob\.IsValid && action\.ClassJob\.RowId == EnemyCombatConstants\.ScholarJobId && action\.ClassJobCategory\.IsValid && action\.ClassJobCategory\.RowId == 29 && action\.ActionCategory\.IsValid && action\.ActionCategory\.RowId == 4 && action\.Range == EnemyCombatConstants\.ScholarCriticalStrategySheetRange && action\.EffectRange == 0 && action\.Cast100ms == 0 && action\.Recast100ms == EnemyCombatConstants\.ScholarCriticalStrategyRecast100ms && action\.PrimaryCostType == 0 && action\.PrimaryCostValue == 0 && action\.CooldownGroup == 3 && action\.MaxCharges == 0 && !action\.CanTargetSelf && !action\.CanTargetParty && !action\.CanTargetAlliance && action\.CanTargetHostile && !action\.CanTargetAlly && !action\.CanTargetOwnPet && !action\.CanTargetPartyPet && !action\.TargetArea && action\.RequiresLineOfSight && action\.NeedToFaceTarget && !action\.AffectsPosition && action\.CastType == 1 && description\.Contains\("Increases target''s damage taken by 10%", StringComparison\.Ordinal\) && description\.Contains\( "Halves the defensive bonus of Guard instead when targeting enemies under its effect\.", StringComparison\.Ordinal\); \}\);') {
    throw 'SCH Critical Strategy must fail closed unless exact English action/transient metadata proves PvP SCH 29716, icon 9284, 25y single-hostile targeting, 20s recast, and the Guard-specific effect.'
}
if ($normalizedMetadata -notmatch 'var silentNocturneVerified = ValidateFeature\("Silent Nocturne", log, \(\) => \{.*?actions\.TryGetRow\(EnemyCombatConstants\.SilentNocturneActionId, out var action\).*?action\.Name\.ToString\(\), "Silent Nocturne", StringComparison\.Ordinal\).*?action\.Icon == EnemyCombatConstants\.SilentNocturneActionIconId.*?action\.ClassJob\.RowId == EnemyCombatConstants\.BardJobId.*?action\.Range == EnemyCombatConstants\.SilentNocturneRange.*?action\.CanTargetHostile.*?action\.RequiresLineOfSight.*?"Silences target\."') {
    throw 'BRD Silent Nocturne must fail closed unless exact English PvP BRD metadata proves action 29395, icon 9627, hostile line-of-sight targeting, and its separate 20-yalm native range.'
}

$exactCombatIds = [ordered]@{
    GuardActionId = 29054
    GuardianActionId = 29066
    GuardianIconId = 9584
    PaladinJobId = 19
    GuardianRecast100ms = 300
    GuardianSheetRange = 20
    ScholarCriticalStrategyActionId = 29716
    ScholarCriticalStrategyIconId = 9284
    ScholarJobId = 28
    ScholarCriticalStrategyRecast100ms = 200
    ScholarCriticalStrategySheetRange = 25
    WildfireActionId = 29409
    WildfireStatusId = 1323
    DeathWarrantActionId = 29549
    DeathWarrantStatusId = 3206
    MarksmanSpiteActionId = 29415
    MarksmanSpiteIconId = 9636
    MarksmanSpiteTimelineId = 11546
    ZantetsukenActionId = 29537
    ZantetsukenIconId = 9666
    ZantetsukenRecast100ms = 100
    SamuraiJobId = 34
    FuriousBacklashActionId = 39188
    FuriousBacklashIconId = 9730
    FuriousBacklashRecast100ms = 20
    ViperJobId = 41
    HardenedScalesStatusId = 4096
    InnerReleaseStatusId = 1303
    MeikyoShisuiStatusId = 1320
    MiracleOfNatureActionId = 29228
    MiracleOfNatureActionIconId = 9608
    WhiteMageJobId = 24
    MiracleOfNatureRecast100ms = 240
    SilentNocturneActionId = 29395
    SilentNocturneActionIconId = 9627
    BardJobId = 23
    SilentNocturneRecast100ms = 200
    ContradanceActionId = 29432
    ContradanceActionIconId = 9641
    DancerJobId = 38
    ContradanceRecast100ms = 100
    SeducedStatusId = 3024
    SeducedStatusIconId = 214889
    PvPStunStatusId = 1343
    PvPHeavyStatusId = 1344
    PvPBindStatusId = 1345
    PvPSilenceStatusId = 1347
    DeepFreezeStatusId = 3219
    MiracleOfNatureStatusId = 3085
    PurifyActionId = 29056
    ResilienceStatusId = 3248
}
foreach ($entry in $exactCombatIds.GetEnumerator()) {
    if ($combatConstants -notmatch "\b$([regex]::Escape($entry.Key))\s*=\s*$($entry.Value)\s*;") {
        throw "Patch 7.5 metadata ID drifted: $($entry.Key) must be $($entry.Value)."
    }
}
if ($metadataGuard -notmatch 'guardian\.Range\s*==\s*EnemyCombatConstants\.GuardianSheetRange' -or
    $defensiveUtility -notmatch 'action\.Range\s*==\s*EnemyCombatConstants\.GuardianSheetRange') {
    throw 'Both Guardian metadata boundaries must retain the exact verified sheet range of 20 yalms.'
}

$personalDefinitionsPath = Join-Path $pluginServicesRoot 'PersonalStatusDefinition.cs'
$personalDefinitions = Read-RequiredSource $personalDefinitionsPath 'Personal status definitions'
$personalStatus = Read-RequiredSource $personalStatusPath 'Personal status service'
Assert-Literals ($personalStatus + $personalDefinitions) @(
    'EnemyCombatConstants.WildfireStatusId',
    'EnemyCombatConstants.DeathWarrantStatusId',
    'EnemyCombatConstants.MarksmanSpiteActionId',
    'EnemyCombatConstants.PvPStunStatusId',
    'EnemyCombatConstants.PvPHeavyStatusId',
    'EnemyCombatConstants.PvPBindStatusId',
    'EnemyCombatConstants.PvPSilenceStatusId',
    'EnemyCombatConstants.DeepFreezeStatusId',
    'EnemyCombatConstants.MiracleOfNatureStatusId'
) 'Personal status exact-ID mapping'
Assert-Literals $personalStatus @(
    'IKeyState',
    'PersonalDebuffAlertRules.Observe',
    'PersonalStatusDefinitions.Find',
    'PersonalStatusDefinitions.IsMetadataVerified',
    'CanTriggerPurifyBuffer',
    'new EmergencyActionInputCoordinator(keyState)',
    'new EmergencyPurifyProbe(log)',
    'new DefensiveUtilityProbe(',
    'new AllyRescueProbe(',
    'emergencyPurify.Observe',
    'defensiveUtility.ObserveGuard',
    'smartRecuperate.Observe',
    'defensiveUtility.ObserveGuardian',
    'allyRescue.Observe',
    'shouldScanStatuses',
    'configuration.ExperimentalPurifyOnNextKey',
    'configuration.PurifyOnStun',
    'configuration.PurifyOnHeavy',
    'configuration.PurifyOnBind',
    'configuration.PurifyOnSilence',
    'configuration.PurifyOnDeepFreeze',
    'configuration.PurifyOnMiracleOfNature',
    'configuration.PurifyOnHeldGameplayKey',
    'configuration.ExperimentalAllyRescueOnNextKey',
    'configuration.AllyRescueOnHeldGameplayKey',
    'IsPurifyAutomationEnabled',
    'EnemyCombatConstants.ResilienceStatusId',
    'purifyStatusCurrentlyObserved',
    'StatusIdentityState',
    'PersonalDebuffAlertRules.MissingGraceMilliseconds',
    'DebouncedVisibilityRules.Observe',
    'resiliencePresence.IsVisible',
    'PvPMatchRules.ResolveSupportedContext',
    'configuration.EnableWolvesDenTesting'
) 'Personal status service'
if ($personalStatus -match 'WolvesDenOpponentResolver\.Resolve') {
    throw 'Self warnings and self-Purify must not depend on resolving an enemy HUD actor.'
}

$stateAssignment = [regex]::Match(
    $purifyProbe,
    '(?m)^\s*(?:this\.)?[A-Za-z_]\w*\s*=\s*[A-Za-z_]\w*\.NextState\s*;')
$tryUsePurify = [regex]::Match($purifyProbe, '\bTryUsePurify\s*\(')
if (-not $stateAssignment.Success -or -not $tryUsePurify.Success -or $stateAssignment.Index -gt $tryUsePurify.Index) {
    throw 'Emergency Purify runtime must assign the decision NextState before calling TryUsePurify.'
}
$consumeHeldInput = [regex]::Match(
    $purifyProbe.Substring($stateAssignment.Index),
    '\binputFrame\.Consume\s*\(')
$consumeHeldInputIndex = if ($consumeHeldInput.Success) {
    $stateAssignment.Index + $consumeHeldInput.Index
} else {
    -1
}
if (-not $consumeHeldInput.Success -or
    $stateAssignment.Index -gt $consumeHeldInputIndex -or
    $consumeHeldInputIndex -gt $tryUsePurify.Index) {
    throw 'Emergency Purify must store state and claim the scheduler frame before attempting Purify.'
}
if ([regex]::Matches($purifyProbe, '\bTryUsePurify\s*\(').Count -ne 2) {
    throw 'Emergency Purify probe must have one TryUsePurify call site and one method definition.'
}

if ($personalStatus -match '\b(SetRawValue|ClearAll)\b' -or
    $inputContext -match '\b(SetRawValue|ClearAll)\b' -or
    $inputContext -match '(?:this\.)?keyState\s*\[[^\]]+\]\s*=') {
    throw 'Personal status input path may read IKeyState but must never mutate it.'
}
$keyStateCalls = [regex]::Matches($inputContext, '(?:this\.)?keyState\.(?<Method>[A-Za-z_]\w*)\s*\(')
foreach ($call in $keyStateCalls) {
    if ($call.Groups['Method'].Value -notin @('GetRawValue', 'GetValidVirtualKeys')) {
        throw "Personal status input path uses a non-read IKeyState method: $($call.Groups['Method'].Value)."
    }
}
if ($inputContext -notmatch '(?:this\.)?keyState\s*\[[^\]]+\]' -and
    $inputContext -notmatch '(?:this\.)?keyState\.GetRawValue\s*\(') {
    throw 'Personal status input path must prove a read-only IKeyState sample.'
}

$purifyRules = Read-RequiredSource (Join-Path $coreRoot 'EmergencyPurifyBufferRules.cs') 'Emergency Purify buffer rules'
Assert-Literals $purifyRules @(
    'WaitingForFreshKey',
    'SpentUntilStatusGone',
    'DefaultBufferMilliseconds = 750',
    'MinimumBufferMilliseconds = 100',
    'MaximumBufferMilliseconds = 1_000',
    'LocalPlayerIdentityInvalid',
    'ResilienceActive',
    'CancelAndWaitIfPresent',
    'ArmOrDispatch',
    'ResolveStatusEntryTrigger',
    'HeldKeyAtStatusEntry',
    'AllowHeldKeyAtStatusEntry',
    'public bool ShouldDispatch => Kind == EmergencyPurifyBufferDecisionKind.Dispatch',
    'public bool ShouldConsumeInputGeneration'
) 'Emergency Purify buffer rules'
if ([regex]::Matches($purifyRules, '\bResolveStatusEntryTrigger\s*\(').Count -ne 3) {
    throw 'Held-key level must be resolved only for initial status entry, status replacement, and its method definition.'
}

$physicalKeyRules = Read-RequiredSource (Join-Path $coreRoot 'PhysicalGameplayKeyRules.cs') 'Physical gameplay key rules'
Assert-Literals $physicalKeyRules @(
    'A key that is already down when observation starts is not new player',
    'previous.IsConsumed || pressedWhileTyping',
    'isFreshPress && !pressedWhileTyping',
    'eligible && !consumed && !pressedWhileTyping',
    'public static PhysicalGameplayKeyState Consume'
) 'Physical gameplay key generation rules'

$projectFile = Read-RequiredSource (Join-Path $sourceRoot 'SeitonSense.Plugin\SeitonSense.Plugin.csproj') 'Plugin project'
$pluginManifest = Read-RequiredSource (Join-Path $sourceRoot 'SeitonSense.Plugin\SeitonSense.Plugin.json') 'Plugin manifest'
$repositoryIndex = Read-RequiredSource (Join-Path $resolvedRoot 'repo.json') 'Custom repository index'
Assert-Literals $projectFile @(
    '<Version>0.30.0.0</Version>',
    '<AssemblyVersion>0.30.0.0</AssemblyVersion>',
    '<FileVersion>0.30.0.0</FileVersion>'
) 'v0.30.0.0 project version'
Assert-Literals $pluginManifest @(
    'Exact PvP nameplate/LB cues, reliable held helpers, Smart Target, and survival tools.',
    'exact native-nameplate cues',
    'LB activation and damage notifications',
    'Smart Target and assist macros',
    'local MP sounds',
    'focus-target',
    'dark-knight',
    '"sage"',
    '"smart-target"',
    '"nameplate"',
    '"limit-break"',
    '"targeting"',
    '"survival"'
) 'v0.30.0.0 plugin manifest metadata'
if ($pluginManifest -match 'combat frames|combat-frames|calibrated LB gauges|row targeting and mouseover') {
    throw 'Current plugin metadata must not advertise the retired Combat Frames runtime.'
}
Assert-Literals $repositoryIndex @(
    '"AssemblyVersion": "0.30.0.0"',
    'visible /autoseiton ON/OFF tile',
    'Smart Target',
    'Retires Combat Frames',
    'enemy LB icons/durations to native nameplates',
    'local 4000/2000 MP sounds',
    'one-time What''s New popup',
    'schema 32',
    '"IsHide": false'
) 'v0.30.0.0 custom-repository metadata'
if ($repositoryIndex -notmatch '"LastUpdate"\s*:\s*"\d+"' -or
    [regex]::Matches($repositoryIndex, '"LastUpdate"').Count -ne 1) {
    throw 'The custom repository entry must retain one numeric LastUpdate field without pinning its release-time value.'
}
Assert-Literals $combatConstants @(
    'MiracleOfNatureRange = 10f',
    'SilentNocturneRange = 20f'
) 'Action-specific WHM Miracle and BRD Silent Nocturne native ranges'
$readme = Read-RequiredSource (Join-Path $resolvedRoot 'README.md') 'README'
$changelog = Read-RequiredSource (Join-Path $resolvedRoot 'CHANGELOG.md') 'Changelog'
$privacy = Read-RequiredSource (Join-Path $resolvedRoot 'PRIVACY.md') 'Privacy documentation'
$normalizedReadme = $readme -replace '\s+', ' '
$normalizedChangelog = $changelog -replace '\s+', ' '
$normalizedPrivacy = $privacy -replace '\s+', ' '
Assert-Literals $normalizedReadme @(
    'Version 0.30.0.0 retires the unusable fixed Combat Frames runtime and its click/mouseover and calibrated-gauge paths',
    'exact enemy nameplate icons, a safe self activation banner, and a bounded ally damage feed',
    'Smart Target macros',
    'visible `/autoseiton` ON/OFF tile that still requires a physical held key',
    'local 4,000/2,000-MP sounds',
    'version-acknowledged What''s New window',
    '**Purify > NIN Seiton > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH Critical Strategy > DRK Hiebsprung > Smart Recuperate > generic Guard > pressure Sprint > event Kardia > event Monk**',
    'original critical boundary remains unconditional at 20% HP',
    'target count of at least three enemies may trigger the same frozen rescue earlier, at 35% HP or lower',
    'both central `UseAction` and `UseActionLocation` hooks are enabled',
    'dedicated `/panicshu` scope releases this ownership before forwarding its location call',
    'Configuration schema 32 is current in v0.30.0.0',
    'https://raw.githubusercontent.com/kittenhaswares-ui/SeitonSense/main/repo.json'
) 'v0.30.0.0 current README release and safety contract'
Assert-Literals $normalizedChangelog @(
    '## 0.30.0.0',
    'Ongoing physical held-key consent remains required',
    'Purify stays first and enabled NIN Seiton now gets the next scheduler slot',
    'Added `/smarttab` (`/sstarget`)',
    'non-extending 3-second held lease',
    'original `<=20%` rescue remains unconditional',
    '`21-35%` is eligible only from a fresh exact `3+` incoming hard/cast-target count',
    'Removed the unusable fixed Combat Frames',
    'local-player MP sounds at downward crossings of `4,000` and `2,000`',
    'one-time What''s New window',
    'plugin to `0.30.0.0` and configuration schema to `32`',
    'repository listing is visible again',
    'All `388` Core tests pass'
) 'v0.30.0.0 release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.29.0.1',
    'Guardian''s target-side `Covered` / `Gedeckt` status',
    'Phalanx `Hallowed Ground` self-invulnerability',
    'Eventide''s `Undead Redemption` HP-floor status',
    'Guard / Wehr itself remains a valid Seiton target',
    'before initial ranking, every frozen retry, optional held-cast cancellation, and the latest safe native request boundary',
    'without `UseAction`, reranking, an alternate target, or reopening the same Unsealed follow-up',
    'metadata is validated fail-closed',
    'all `367` Core tests pass',
    'plugin version to `0.29.0.1`',
    'Configuration schema remains `31`',
    'manual Seiton plus `/panicshu` are unchanged'
) 'v0.29.0.1 Auto-Seiton protection hotfix release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.29.0.0',
    'separate default-off NIN held helper for exact Crystalline Conflict',
    'strictly below `20%` HP',
    'live Guard / Wehr status `3054` or `3673`',
    'freezes one exact actor and calls ground-targeted Shukuchi `29513` at that actor''s latest revalidated position',
    'Positive fresh team pressure is an optional ranking bonus',
    'hard-targets that exact same living enemy once',
    '`/panicshu` remains the sole own-Guard-breaking exception',
    'after PLD Guardian and before NIN Seiton',
    'real cooldown-unavailable to ready epoch',
    'configuration schema to `31`',
    'off for new, upgraded, and reset configs'
) 'v0.29.0.0 NIN Guard-Shukuchi release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.28.0.1',
    'Simplified the explicit manual NIN `/panicshu` macro into one immediate action path',
    'calls native Shukuchi at most once in the command callback',
    '500-ms lease, pending state, framework wait, expiry, and second-command preservation were removed',
    'allows the manual command from the local player''s own Guard',
    'no longer consults the held-action scheduler, Self-Purify priority, crowd-control state, cast, native queue, animation lock, cooldown, or resource readiness',
    'FFXIV immediately accepts or rejects that one request',
    'later macro press is a new explicit command, never an automatic retry',
    'exact adjusted Shukuchi `29513` Doton block',
    'no target/cursor mutation, shorter fallback, alternate action, or replay',
    'Routine command results are now chat-silent',
    'Bumped the plugin version to `0.28.0.1`',
    'Configuration schema remains `30`'
) 'v0.28.0.1 immediate manual Panic Shukuchi release notes'
Assert-Literals $normalizedReadme @(
    '## Manual NIN Panic Shukuchi macro',
    'explicit one-line macro command, not an automatic feature and not part of the held-action scheduler',
    '/panicshu',
    'exact PvP Ninja in Crystalline Conflict, or in the Wolves'' Den when the existing **Enable Wolves'' Den testing** option is enabled',
    'projects only the point 19.5 yalms straight ahead onto terrain, and immediately makes at most one native location-action call in that same command callback',
    'no stored intent, scheduler claim, 500-ms lease, wait, expiry, cast/queue/animation-lock gate, cooldown/resource precheck, Guard gate, or crowd-control/Purify-priority gate',
    'FFXIV decides whether the one request is accepted in the current state',
    'later macro press is a new explicit command rather than a replay',
    'Three Mudra changes Shukuchi into Doton',
    'no retry, alternate action, shorter/inward point, path search, or destination fallback',
    'never moves the mouse or ground-target cursor and never reads, changes, or substitutes a hard, soft, Focus, or mouseover target',
    'Routine accepted/rejected results are chat-silent and remain available only through `/seiton debug` diagnostics'
) 'v0.28.0.1 Panic Shukuchi immediate command, own-Guard, no-state, and no-target README contract'
Assert-Literals $normalizedPrivacy @(
    '## Explicit manual NIN Panic Shukuchi macro',
    '`/panicshu` is a command-only, user-authored macro action',
    'no automatic, pressure, enemy, status, or held-key trigger',
    'terrain point 19.5 yalms along the local character''s current facing',
    'immediately makes at most one native location-action call in the same command callback',
    'stores no pending intent and has no lease, timer, framework wait, expiry, scheduler/Purify claim, Guard or crowd-control gate, cast/queue/animation-lock gate, or cooldown/resource precheck',
    'intentionally allowed from own Guard so Shukuchi may break it',
    'anything other than exact Shukuchi `29513` still blocks the attempt',
    'later macro press is a new explicit user command',
    'does not recompute after movement or turning, search a path, move inward, choose an alternate action, or use a shorter fallback point',
    'neither reads nor changes the mouse/ground-target cursor or any hard, soft, Focus, or mouseover target',
    'last origin/destination coordinates, native acceptance outcome, and aggregate command counters may remain in plugin memory',
    'not persisted or uploaded',
    'Four-direction, slope, wall, and invalid-endpoint tests in the Wolves'' Den remain a live-validation boundary',
    'Configuration schema 32 is current in v0.30.0.0'
) 'v0.29.0.0 Panic Shukuchi retained transient-data, immediate, own-Guard, no-target, and live-boundary privacy contract'
Assert-Literals $normalizedChangelog @(
    '## 0.27.1.0',
    'Fixed the v0.27 reactive held-key regression without widening any event deadline',
    'urgent startup now remembers its exact actor, action, and event first',
    'attach the first currently eligible held/fresh key generation inside the original bounded threat lease',
    'Post-Purify and post-Guard remember the exact enemy episode while protection is live',
    'bind the current eligible generation only when authoritative Resilience/Guard absence opens the original 500-ms release opportunity',
    'Once attached, the key is strict',
    'Expired/disabled leases retire and every active startup revalidates its frozen job, action, and actor before new packets are drained',
    'exact later urgent startup may preempt only an unattempted lower-priority reactive lease',
    'exact self-target Purify action packet when it omits an individual recovered-status tuple',
    'Live Resilience is still mandatory',
    'only that already-deduplicated signal is retained for resolution inside its original 750-ms acquisition deadline',
    'no key, target fallback, action, or deadline extension is created',
    'Native range/line of sight and blocker state are now checked before simultaneous protection-end candidates are ranked',
    'Extended only NIN post-protection reactive intent lifetime to 3,000 ms',
    '2.5-second recast',
    'WHM and BRD retain the normal 1.5-second held-action lease',
    'exact `SourceSequence` produced by the plugin''s accepted native request',
    'manual Miracle, Silent Nocturne, Raiju, Paean, or Aquaveil can no longer claim',
    'Bumped the plugin version to `0.27.1.0`',
    'configuration schema remains `30`'
) 'v0.27.1.0 reactive reliability, exact attribution, version, and unchanged schema release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.27.0.0',
    'Added event-owned reservations to reactive counter-CC',
    'eligible physical key, exact canonical actor, local counter action, and event epoch are frozen when an urgent startup, enemy Purify, or first enemy Guard presence is observed',
    'A later key cannot inherit that episode',
    'release, text-input poisoning, identity drift, or ambiguity is terminal with no alternate, target switch, fallback, or replay',
    'Validated live `RemainingTime` may establish only a non-extending expected Resilience/Guard end',
    'Real status-list absence remains mandatory',
    'Post-Purify can use the first authoritative absent frame at or after its expected end',
    'early or untimed absence keeps the 150-ms anti-flicker proof',
    'Post-Guard still releases on its first authoritative absent frame, including an early manual cancel',
    'released Guard reservation stays retired through ambiguous samples until exact absence separates a later Guard episode',
    'Forked Raiju `29510` and Fleeting Raiju `29707`',
    'standard Purify-removable protection matrix',
    'confirmed only by exact Stun `1343` on the frozen enemy',
    'Both metadata rows must verify before NIN can arm',
    'PvP Spinning Edge/Aeolian Edge Combo carrier `29500`',
    'Sealed Forked Raiju status `3195` is active',
    'both variants wait through exact local Bind `1345`',
    'MCH/SAM/VPR first, DNC second, protection-end releases third',
    'stable event time and canonical identity',
    'Simultaneous losers are terminal',
    'Reactive observation may remain alive while own Guard suppresses every action request',
    'Bumped the plugin version to `0.27.0.0`',
    'Configuration schema remains `30`',
    'there is no new setting or migration, and all existing opt-ins are preserved'
) 'v0.27.0.0 event-owned reservation, advisory timing, NIN, arbitration, and schema release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.26.0.0',
    'Raised every job-specific physical-hold helper into the second priority tier, immediately after Purify',
    '**reactive counter-CC > Ally Rescue > PLD Guardian > NIN Seiton > SCH Critical Strategy > DRK Hiebsprung**',
    'Reactive WHM Miracle / BRD Silent Nocturne wins before ally cleanse because its LB, post-Purify, and post-Guard windows are shorter',
    'complete request order is now **Purify > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Seiton > SCH Critical Strategy > DRK Hiebsprung > Smart Recuperate > generic Guard > pressure Sprint > event Kardia > event Monk**',
    'One continuously held key may still authorize later distinct exact episodes',
    'at most one held helper crosses the native action boundary per framework frame',
    'team pressure an optional positive-only ranking bonus for simultaneous post-Purify and post-Guard counter-CC releases',
    'fresh exact count above zero ranks ahead; zero, unknown, or stale pressure is neutral and never gates a candidate',
    'Remaining order is lowest HP ratio, lowest trusted MP ratio, then stable canonical identity',
    'selected episode remains frozen with no rerank, alternate, target change, fallback, or replay',
    'Bumped the plugin version to `0.26.0.0`',
    'Configuration schema remains `30`',
    'there is no new setting or migration, and existing opt-ins are preserved'
) 'v0.26.0.0 scheduler, optional-pressure, frozen-intent, version, and schema release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.25.0.0',
    'Fixed the v0.24 held-key lease regression across all ten physical-hold helpers',
    'already-held movement key now wins before another stable held key',
    'fresh movement or other gameplay key is used only as fallback',
    'Each frozen intent keeps that exact lease until release, ineligibility, reset, or its action-specific terminal outcome',
    'Added a separate default-off **cancel my active cast for an otherwise-ready held helper** test toggle for exactly the ten physical-hold helpers above',
    'highest-priority frozen intent is otherwise ready and only the local cast blocks it',
    'native cast cancellation once for that observed cast',
    'void native call proves only that cancellation was requested, not that the cast stopped',
    'Cast cancellation and the held action can never occur in the same framework frame',
    'later frame must observe the cast cleared and repeat the complete ordinary helper preflight before any `UseAction` request',
    'synthesizes no movement or Escape input, clears no native queue, writes no cast state, and never changes a target',
    'Smart Kardia, Monk Earth''s Reply, every already- incoming manual/Turbo redirect (including Paean), and macro helpers are excluded',
    'mobile BRD Powerful Shot / MCH Blast Charge',
    'selected cast- cancellation helper, exact action/target/key/intent epoch, observed cast, one- request latch, native request/fault counts, and last result',
    'configuration schema to `30`',
    'cast-cancellation test is explicit opt-in and remains off for fresh, reset, and migrated configurations'
) 'v0.25.0.0 held-lease and default-off cast-cancel release notes'
Assert-Literals $normalizedPrivacy @(
    '## Experimental held-action cast cancellation',
    'This separate test is disabled by default',
    'exact physical-hold intents for Purify, NIN Seiton, reactive counter-CC, Ally Rescue, Guardian, NIN Guard-Shukuchi, SCH Critical Strategy, DRK Hiebsprung, Smart Recuperate, Guard, and pressure Sprint',
    'Smart Kardia, Monk Earth''s Reply, every already-incoming manual/Turbo redirect (including Paean), and macro helpers are excluded',
    'highest-priority eligible intent',
    'rechecks exact local and target identity, held key, context, own Guard, helper action/readiness/resources, empty queue, and nonblocking animation lock',
    'Only when both local cast signals prove an active cast may it request FFXIV''s native cast cancellation once for that observed cast epoch',
    'native function returns no acceptance value',
    'recorded request does not confirm that FFXIV canceled the cast',
    'Signal mismatch, cast-action drift without a fully observed clear state, or other ambiguity fails closed for that epoch',
    'cancellation request claims its framework frame and can never be paired with a helper action request in that frame',
    'later frame must observe both cast signals clear and repeat the ordinary complete helper preflight',
    'does not synthesize movement or Escape, clear the native queue, write cast state, or change a target',
    'current-patch stationary plus mobile BRD/MCH behavior still requires live validation',
    'only the current cast decision, the last requested helper/action/target/key/ intent and native request result, plus request/fault counts in memory',
    'none is persisted or uploaded',
    'Configuration schema 32 is current in v0.30.0.0',
    'NIN Guard-Shukuchi held- key option is forced off for upgrading configurations and remains off for fresh and Reset Defaults configurations',
    'held-action cast-cancellation test remains explicitly off for fresh, reset, and migrated configurations'
) 'v0.27.1.0 held cast cancellation privacy and persistent bounded diagnostics disclosure'
Assert-Literals $normalizedReadme @(
    'Version 0.30.0.0 retires the unusable fixed Combat Frames runtime',
    'Reactive urgent-startup events may bind the first eligible current generation inside the original short threat lease',
    'Authoritative protection end opens a strict, non-extending 500-ms key-acquisition edge',
    'When an eligible current key is acquired inside that edge, exactly one actor/key intent freezes',
    'three seconds from the original release',
    'Binding never restarts that deadline',
    'no different key can inherit the frozen intent',
    '**Stable held-action leases:** Purify, NIN Seiton, reactive counter-CC',
    '**Experimental held-action cast cancellation:** a separate default-off test',
    'known cooldown/resource/cast/queue/full-animation-lock states spend no attempt',
    'only a clean explicit client rejection can retry the same frozen intent after 50 ms with eight calls maximum',
    'Client acceptance, exceptions, uncertain queue/sequence transitions, key release, context/job/ identity drift, and other ambiguity are terminal',
    'Every bound protection-end intent expires three seconds after its original release',
    'Guard retires every simultaneous loser before a higher-priority wait',
    '**Experimental Dark Knight Hiebsprung helper:** a separate default-off held- key option considers only exact canonical `S1`-`S5` enemies at 30% HP or lower',
    'continuous hold can authorize one frozen intent per proven ready epoch',
    'clean rejection uses only the shared bounded retry',
    '**Experimental Sage Smart Kardia helper:** a separate default-off option arms only after the existing Eukrasia call is forwarded unchanged and accepted by the client',
    'Inside that two-second opportunity it requires causal Eukrasia charge/status evidence, an animation-lock-clear Kardia boundary, and a fresh, complete exact five-player pressure view',
    '**Experimental Smart Recuperate helper:** a separate default-off held-key option can use exact self Recuperate `29711` when at least 16,000 HP is missing and at least 2,000 MP is available',
    'cooldown, MP, cast, queue, or animation-lock shortage waits without consuming the held consent',
    'An explicit client rejection may retry only the same exact self epoch',
    '**Experimental Paladin Guardian job tool:** an independent default-off held-key option can attempt Guardian on one exact reachable ally',
    'large fixed red `FOCUSED xN` card at the top center',
    'An older explicitly enabled NIN fresh-edge helper still traverses schema 29 and migrates to the replacement held-key option',
    'Every other existing master and helper choice is preserved',
    'Fresh and reset configurations keep every action-helper master off',
    'three or more exact current hard/cast targets',
    'large fixed red `FOCUSED xN` card',
    'at the top center',
    'only on a narrow work area where the',
    'two scaled cards would overlap is isolation stacked vertically below it',
    'selectable built-in FFXIV system',
    'fires once on entry rather than every frame',
    'separate default-off option may use continuous held WASD/arrow movement-key consent',
    'movement key still reaches',
    'any later native PvP action ends Sprint',
    'urgent high-pressure alarm deliberately uses the narrower direct-intent',
    'Recent harmful-action evidence',
    'cannot start or sustain this warning, its sound, or pressure',
    'Unknown/stale pressure hides the card immediately but cannot manufacture a new',
    'only a continuously known below-three separation can rearm it',
    'The separate **Smart Bard Paean pressure redirect**',
    'disabled by default and',
    'runs only for PvP Bard in exact Crystalline Conflict',
    'already incoming The Warden''s Paean `29400` ability call',
    'complete, unique, stable',
    'living, targetable, non-self party member',
    'native 30-yalm',
    'without the live Warden''s Paean ward `3143`',
    'trusted current count of at least three unique live enemies',
    'higher incoming pressure wins, then lower',
    'exact HP ratio, party slot, entity ID, and game-object ID',
    'forwards the original',
    'target and ability call unchanged as vanilla behavior',
    'pressure drift suppresses that one call',
    'deliberately no cooldown/readiness gate',
    'fall back to the original target, select another ally, or retry',
    'client-accepted return is dispatch feedback only',
    '**Experimental Ally Rescue:** on BRD or WHM, one fresh or explicitly eligible held gameplay key can keep consent active for Paean or Aquaveil on an exact party member',
    'A matching explicit client rejection retains only that frozen status/actor intent for a bounded retry',
    'latest-safe frozen-actor HP re-read, exact-50% cancellation',
    'has no hard-target dependency',
    'both native empty-marker representations',
    '`0xE0000000`',
    '## Scholar Critical Strategy held-key helper',
    'runs only on PvP Scholar in exact Crystalline Conflict',
    'native 25-yalm action range and line-of-sight check immediately before dispatch',
    'If every eligible guarded candidate has an active exact non-negative team-',
    'or negative pressure, or every count is zero, the whole candidate set ranks by',
    'Pressure is used only for this one selection and is not a final',
    'Continuous held consent can produce a frozen Critical Strategy intent for a distinct eligible episode',
    'Only an explicit client rejection may retry the same frozen intent under the shared 50-ms/eight- call policy',
    'Pressure drift',
    'neither reranks nor switches or invalidates the frozen target',
    'not swallow the original key.',
    'current-patch live-confirmation boundaries',
    'uses Wunder der Natur / Miracle of Nature `29228` at native 10-yalm range',
    'BRD it uses Stumme Nocturne / Silent Nocturne `29395` at native 20-yalm range',
    'on NIN it resolves the PvP Spinning Edge/Aeolian Edge Combo carrier `29500` to either Forked Raiju `29510` or Fleeting Raiju `29707` at native 20-yalm range',
    'Both Raiju metadata rows must verify before NIN can arm, and the carrier must expose the exact variant before an action can be requested',
    'Forked Raiju remains blocked while the exact local Sealed Forked Raiju status `3195` is present',
    'both variants remain blocked through exact local Bind `1345`',
    'exact enemy self-Purify `29056` action packet even when that packet omits an individual recovered-status tuple',
    'positive live Resilience `3248`',
    'Purify observation remembers the exact actor, action, episode, and a validated bounded `RemainingTime` hint',
    'does not freeze whichever key happened to be down on that packet frame',
    'Live Resilience membership remains authoritative',
    'first real absent frame at or after the non-extending expected end is eligible immediately',
    'an early or untimed absence still needs 150 ms of continuous proof',
    'already-deduplicated signal may retry only that identity resolution inside its original 750-ms acquisition deadline',
    'It carries no key or action, cannot select another actor, and cannot extend or replay the signal',
    'may bind the current eligible held/fresh generation at that authoritative end or during the same original 500-ms release opportunity',
    'dispatches directly to that actor. It neither requires nor changes the selected target',
    'There is no minimum team-pressure count',
    'Post-Purify state is tracked independently for each canonical `S1`-`S5` slot',
    'exact Guard `3054` or `3673` to be observed present on one canonical `S1`-`S5` actor',
    'Its first exact presence remembers the actor, action, episode, and bounded non-extending duration hint',
    'without freezing an event-edge key',
    'The first verified framework observation that finds Guard absent',
    'including an early manual Guard cancel',
    'may bind the current eligible held/fresh generation on that observation or inside the same original 500-ms release opportunity',
    'Once bound, releasing that key retires the intent',
    'uses the frozen actor directly at the job-specific native range and line of sight',
    'without requiring or switching the selected target, choosing an alternate action/actor, or replaying',
    'Only an explicit client rejection may retry that same frozen intent under the common bound',
    'native range/line of sight, blocker, cast, queue, and animation state are revalidated as dispatcher wait gates',
    'only a fresh exact team-pressure count above zero earns a ranking bonus',
    'higher positive counts first. Known zero, unknown, or stale pressure is neutral and never gates a candidate',
    'Lowest HP ratio follows, then lowest trusted MP ratio and stable `S1`-`S5` identity',
    'Every simultaneous loser is terminal and cannot become a fallback attempt',
    'A continuously held eligible gameplay key keeps consent for the selected frozen episode and may also authorize a later distinct episode',
    'Before binding, the current eligible generation may attach only inside that episode''s original bounded opportunity',
    'After binding, release or text input retires that exact generation without substitution',
    'Only an explicit client rejection may retry the same intent under the common bound; acceptance or ambiguity is terminal',
    'Plugin-owned Miracle, Silent Nocturne, and Raiju requests still pass through the final action-specific CC-immunity brake immediately before the native call',
    'Stun `1343` for either NIN Raiju variant',
    'exact `SourceSequence` created by the plugin request',
    'A manual use of the same action cannot claim the pending automatic result',
    'post-Guard defaults on only behind the disabled reactive-counter master'
) 'Current actor-first reactive-CC, LB/Hiebsprung, and retained helper user contract'
Assert-Literals $normalizedReadme @(
    '## Sage Smart Kardia after accepted Eukrasia',
    'separate **Smart Kardia after accepted Eukrasia** experiment is disabled by default',
    'runs only on PvP Sage in exact Crystalline Conflict',
    'first forwards one exact incoming Eukrasia `29258` call unchanged',
    'Only a client-accepted return creates a token tied to the exact local Sage, territory, pre-call charge/status evidence, and acceptance time; that opportunity expires after two seconds',
    'accepted Eukrasia must become causally visible through either a lower exact native charge count or a newly present local-source Eukrasia status',
    'Kardia must resolve exactly to `29264`, be locally ready, and reach an animation-lock-clear boundary',
    'The pressure publication must be newer than the accepted Eukrasia and provide one complete, unique, stable five-player party view',
    'Exact living, targetable self/party candidates with a trusted current count of at least two unique live enemies directly hard-targeting or casting at them are considered first',
    'Eligible candidates rank by higher incoming pressure, then lower exact HP ratio, party slot, network entity ID',
    'If nobody reaches the pressure threshold, exact self is the sole initial fallback; unknown pressure or an incomplete party view cannot manufacture that fallback',
    'If its local-source Kardion state is unknown or already present, the trigger ends without falling through to another actor',
    'token is consumed before the terminal identity, Kardion, pressure/self-fallback, Kardia metadata/readiness, animation-lock, and native-reachability checks',
    'This follow-up has no physical-key generation and requires its own accepted- Eukrasia trigger',
    'In the current request order it follows pressure Sprint and precedes only event Monk',
    'It never changes a hard, soft, focus, or mouseover target',
    'Client acceptance is dispatch feedback only and does not prove that Kardia or Kardion applied',
    'current-patch hook ordering, charge/status evidence, animation lock, native reachability, dispatch, and server behavior require a live CC test'
) 'v0.24.0.0 retained accepted-Eukrasia Smart Kardia causal-token, fresh-pressure, exact priority, direct-target, and live-boundary user contract'
Assert-Literals $normalizedReadme @(
    '## DRK Shadowbringer two-line macro',
    'supports exact PvP Dark Knight in Crystalline Conflict',
    'supports the Wolves'' Den only when the existing **Enable Wolves'' Den testing** option on Start and this DRK helper are both enabled',
    '/seitonbringer',
    '/pvpac "Souleater Combo" <t>',
    'ReAction Macro Queue and Turbo',
    'In Crystalline Conflict, that target must resolve to one exact canonical `S1`-`S5` enemy, exactly as before',
    'In Wolves'' Den, it must instead remain the exact native current hard target and resolve to the live, targetable combat striking dummy with current NameId `541`',
    'freezes and revalidates that dummy''s game-object ID, entity ID, address, object/sub-kind, NameId, and the native hard-target ID',
    'never queries a synthetic `S1`, `<e1>`, or the duel-opponent resolver',
    'never accepts a player, another attackable object, or an alternate target',
    'Frontline and Rival Wings remain blocked',
    'recognizes a new GCD cycle only from a proven exact 2.40-second combo recast restart plus action-sequence change',
    'at most one Shadowbringer attempt for that cycle',
    'remaining-time window is 0.60-0.80 seconds',
    '0.50 seconds or less never triggers Shadowbringer',
    'paired combo call still reaches vanilla unchanged',
    'native 5-yalm combo and 10-yalm Shadowbringer range and line of',
    'Base Shadowbringer requires',
    'more than 12,000 HP',
    'adjusted Dark Arts form requires the exact Dark Arts status/action state',
    'one-attempt token is spent before the final native Shadowbringer',
    'cannot choose another target or action, replay the macro, or retry',
    'never changes the visible hard, soft, or Focus Target',
    '`CLIENT ACCEPTED` is local dispatch feedback, not proof',
    'successful striking-dummy trace checks only the Wolves'' Den path and does not prove live CC timing or execution',
    'exact combo-row secondary cost types `0/58/58/147/147/147`',
    'first native GCD sample is taken from the framework update thread instead of synchronously during plugin startup',
    'striking-dummy NameId metadata does not match, only the Den test path is disabled and canonical CC support remains available',
    '## Optional Auto Low-MP Focus Target',
    'complete, unique native `S1`-`S5` set',
    'trusted enemy MP that remains at 2,000 or lower for 150 ms',
    'clears only after 150 ms continuously at 2,300 MP or higher',
    'native 20-yalm',
    'Lowest exact MP ratio wins, then lowest HP ratio, stable S-slot, and exact actor identity',
    'native Focus Target observed stably empty',
    'never clears, replaces, restores, or retries',
    'manual ownership wins and latches',
    'feeds FFXIV''s Focus Target HUD and `<f>`',
    'independent of the party-visible Attack1 sign',
    'no atomic compare-and-set operation for Focus Target',
    'current-patch live A/B boundaries'
) 'v0.18.0.1 exact Den-dummy/retained CC DRK macro and set-only Auto Low-MP Focus user contract'
Assert-Literals $normalizedChangelog @(
    '## 0.24.0.0',
    'Replaced global one-use held-key consumption with continuous physical-key consent and a shared per-frame priority scheduler',
    'same hold may authorize later distinct exact held episodes',
    'Purify > Smart Recuperate > Ally Rescue > reactive counter-CC > Guard > Guardian > pressure Sprint > Kardia > NIN > SCH > Monk > DRK Hiebsprung',
    'at most one held helper crosses the native action boundary in one framework frame',
    'Added a common bounded pre-acceptance contract to every held-action helper',
    'Known cooldown, resource, cast, queued-action, and animation-lock blocks wait without spending the attempt budget',
    'Only an explicit client rejection may retry the same frozen intent after 50 ms, with eight native attempts maximum',
    'client acceptance and ambiguous/exceptional outcomes are terminal',
    'No helper may rerank after freezing, select an alternate, mutate the selected target, or retry an action already accepted by the client',
    'Changed NIN Seiton from a fresh-edge-only helper to the same held scheduler',
    'Preserved accepted Ally Rescue confirmation evidence across later rejected calls',
    'Changed Limit Break activation in Combat Frames to a pulsing outer border and compact icon/name/countdown banner so HP, MP, LB gauge, and status badges stay visible',
    'When Combat Frames are enabled while interaction is off, Settings now shows a prominent state label and a one-click enable button',
    'Bumped the plugin version to `0.24.0.0` and configuration schema to `29`',
    'explicit NIN fresh-edge-to-held migration',
    'Existing action-helper opt-ins otherwise remain unchanged; new and reset action helpers remain default-off'
) 'v0.24.0.0 held scheduler, retry contract, NIN migration, Ally Rescue confirmation, Combat Frames UX, version, and schema release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.21.0.0',
    'Fixed German Paladin Guardian communication to use FFXIV''s canonical localized `/schnellchat <P#> Ziel decken` form',
    'Added optional **Combat Frame interaction**',
    'fresh, living, exact canonical `S1`-`S5` row can be clicked once to set that actor as the hard target',
    'hover can publish that exact actor through FFXIV''s native mouseover slots',
    'Self, preview, dead/unknown rows, stale snapshots, and gaps stay click-through',
    'every click is revalidated once with no retry, and external mouseover ownership wins',
    'Added configurable **Combat Frame Limit Break telemetry**',
    'Self uses the exact native LimitBreakController gauge',
    'Remote `S1`-`S5` gauges remain `LB ?` until the current native HUD instance proves a live calibration against Self',
    'no elapsed-time or job charge-time estimate is used',
    'duration countdowns originate only from a matching live `RemainingTime`',
    'One missing sample of at most 150 ms may preserve the last exact expiry without extending it',
    'Instant LBs use a fixed 1.8-second card',
    'optional **ally LB damage feed** using only direct ActionEffect damage attributed to an exact ally caster and reviewed LB action',
    'does not infer damage from HP deltas and stays silent for pet, periodic, or ambiguous damage',
    'separate default-off **Dark Knight Hiebsprung** held-key helper',
    'canonical enemies at 30% HP or lower',
    'strict 10-yalm center-distance cap plus native range/line of sight',
    'continuous hold may repeat only after an observed not-ready-to-ready cooldown transition',
    'target mutation, alternate, replay, and retry remain forbidden',
    'Expanded BRD Silent Nocturne urgent startup coverage to DNC, MCH, SAM, and VPR at the action''s native 20-yalm range',
    'current request priority is **Purify > Smart Recuperate > Guard > Guardian > pressure Sprint > Ally Rescue > reactive CC > Kardia > NIN > SCH > Monk > Hiebsprung**',
    'Bumped the plugin version to `0.21.0.0` and configuration schema to `27`',
    'Schema-27 migration preserves an existing schema-26 user''s Combat Frames master and helper choices',
    'forces only the new Hiebsprung and interaction leaves off',
    'enables both read-only LB detail leaves behind that existing master choice',
    'Fresh/reset action and Combat Frames masters remain off',
    'interaction and both LB detail leaves default on behind the disabled frame master'
) 'v0.21.0.0 Combat Frames interaction/LB, DRK Hiebsprung, BRD expansion, priority, version, and schema release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.20.0.1',
    'Fixed **Smart Recuperate** remaining blocked after opt-in because the current action-sheet representation exposes the shared PvP Recuperate action''s row-0 `ClassJob` reference as valid',
    'Metadata validation no longer rejects that canonical shared-action representation',
    'Runtime behavior and configuration are otherwise unchanged',
    'remains default-off, exact-Crystalline-Conflict-only, self-only, inclusive at 16,000 missing HP and 2,000 MP',
    'limited to one attempt per eligible held generation',
    'Configuration schema remains `26`'
) 'v0.20.0.1 Smart Recuperate metadata hotfix release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.20.0.0',
    'Added default-off **fixed Combat Frames** for exact Crystalline Conflict',
    'one Self frame and stable `S1`-`S5` enemy rows',
    'non-clickable screen-space overlays; no world projection, target mutation, or native FFXIV HUD edit is performed',
    'Added default-off **Smart Recuperate on held gameplay key** for exact Crystalline Conflict',
    'At exactly 16,000 or more missing HP and at least 2,000 observed MP',
    'Missing MP or native readiness leaves the generation unspent',
    'consumed before final identity, HP, MP, Guard, context, metadata, and readiness revalidation; drift, rejection, or exception never retries',
    'Replaced the frame-driven held-key **Smart Kardia** scanner and six-second throttle with one short-lived opportunity after an incoming PvP Eukrasia',
    'call is forwarded unchanged and returns client-accepted',
    'real local charge decrease or newly observed own-source Eukrasia status must causally confirm that call',
    'Kardia waits for animation lock to clear and requires a fresh complete pressure publication created after acceptance',
    'exact self is the sole initial fallback when nobody meets the threshold',
    'frozen trigger and actor are spent before at most one direct-GOID request',
    'Removed the speculative low-HP **pre-Guard** rule',
    'Moved **Paladin Guardian** to an independent default-off Job Tool',
    'shared physical-input priority is now Self-Purify, Smart Recuperate, reactive Guard, PLD Guardian, pressure Sprint, Ally Rescue, reactive counter-CC, Ninja Seiton, then Scholar Critical Strategy',
    'Accepted-Eukrasia Kardia is a separate bounded follow-up',
    'Bumped the plugin version to `0.20.0.0` and configuration schema to `26`',
    'Smart Recuperate and Combat Frames remain off for fresh, upgrading, and reset configurations',
    'prior explicit Smart Kardia opt-in migrates to the new Eukrasia-triggered mode',
    'only a previously effective Guardian opt-in migrates to the independent Job Tool'
) 'Retained v0.20.0.0 Combat Frames, Survival, accepted-Eukrasia Kardia, Guardian separation, schema, and live-boundary release notes'
Assert-Literals $normalizedChangelog @(
    '## 0.18.0.1',
    'Extended the separate default-off `/seitonbringer` helper to Wolves'' Den striking-dummy testing',
    'requires both the existing DRK Macro Helpers opt-in and the existing Wolves'' Den test option',
    'Frontline and Rival Wings remain blocked',
    'exact live, targetable combat striking dummy with current NameId `541`',
    'object identity and hard target are frozen and revalidated',
    'never uses synthetic `S1`, `<e1>`, the duel-opponent resolver, a player, another attackable object, an alternate target, or a retry',
    'canonical `S1`-`S5` Crystalline Conflict path is unchanged',
    'Retained the proven 2.40-second GCD-cycle token, inclusive 0.60-0.80-second window, spend-before-request ownership',
    'Corrected the cached current-data combo metadata gate to require the exact per-row secondary cost types `0/58/58/147/147/147`',
    'previous all-zero check failed closed',
    'Deferred the first native GCD observation from synchronous plugin startup to the framework update thread',
    'observed off-main-thread local-player lookup failure',
    'Bumped the plugin version to 0.18.0.1. Configuration schema 24 is unchanged',
    'hotfix adds no setting and preserves all existing defaults and migration behavior'
) 'v0.18.0.1 exact Den-dummy DRK hotfix, current metadata, startup thread, unchanged schema, and live boundary'
Assert-Literals $normalizedChangelog @(
    '## 0.18.0.0',
    'default-off **Auto Low-MP Focus Target**',
    'complete unique native `S1`-`S5` view',
    '150 ms of trusted MP at or below 2,000',
    'native 20-yalm range and',
    'line-of-sight result',
    'clears only after 150 ms at or above',
    '2,300 MP',
    'set only an empty local native Focus Target',
    'never clears,',
    'replaces, restores, or retries',
    'manually changed Focus',
    'latches manual',
    'no atomic compare-and-set',
    'default-off **DRK Shadowbringer macro helper**',
    'adjacent lines `/seitonbringer` and `/pvpac "Souleater Combo" <t>`',
    'ReAction Macro Queue and Turbo',
    'one Shadowbringer attempt per proven 2.40-second Souleater Combo GCD',
    'inclusive 0.60-0.80-seconds-remaining window',
    '0.50 seconds or less never triggers Shadowbringer',
    'native 5-yalm combo and 10-yalm',
    'stable queue/action sequencing',
    'more than 12,000 HP or the exact Dark Arts state',
    'one-attempt token is spent before the final request',
    'no target mutation,',
    'alternate, replay, or retry',
    'Client acceptance is not proof of server execution',
    'persistent Macro Helpers and Targets settings pages',
    'Bumped the plugin version to 0.18.0.0 and configuration schema to 24',
    'features remain off for fresh configurations, upgrades, and reset defaults'
) 'v0.18 Auto Low-MP Focus, DRK one-cycle macro, schema, and live-boundary release notes'
Assert-Literals $changelog @(
    '## 0.17.0.0',
    'large fixed top-center **high-pressure warning**',
    'three distinct current enemies directly hard-target or',
    'ordinary counter''s longer recent-harmful-action union',
    'unknown data',
    'cannot rearm a sound episode',
    'continuously known',
    'below-three separation',
    'red `FOCUSED xN` card pulses only its alpha and border',
    'pressure card stays centered while the',
    'separate amber isolation card keeps its own top-left position',
    'selectable built-in FFXIV system sound',
    'No external or Windows audio is used',
    'default-off **Sprint once from a held movement key** option',
    'listens only to WASD/arrow movement keys',
    'does not swallow the original',
    'one-physical-generation/one-action',
    'generation is consumed',
    'alternate, replay, or retry',
    'Bumped the plugin version to 0.17.0.0 and configuration schema to 23',
    'visual warning is enabled for fresh and reset configurations',
    'Sound and Sprint',
    'remain off for fresh, upgraded, and reset settings'
) 'v0.17 high-pressure warning, native sound, held-Sprint, and schema release notes'
Assert-Literals $changelog @(
    '## 0.16.0.0',
    'default-off **Smart Paean target for manual or',
    'Turbo calls** option',
    'already incoming The Warden''s',
    'Paean `29400` ability call',
    'never creates an action or consumes the shared generic input',
    'complete, unique, stable exact party view',
    'live Warden''s Paean ward `3143`',
    'native 30-yalm range and line of',
    'trusted incoming-pressure count of at least three',
    'Higher pressure wins, then lower exact',
    'target and incoming call remain unchanged as vanilla behavior',
    'suppresses that one call rather than falling back',
    'no cooldown',
    'never changes a selected target, substitutes an action, replays,',
    'Hardened the experimental Ninja Seiton helper at its latest safe dispatch',
    're-resolves only the frozen `S#` actor and re-reads that exact actor''s HP',
    'healing to exactly 50% or higher cancels the spent attempt with no alternate',
    'unused native marker slot as `0xE0000000` instead of `0`',
    'Both native empty',
    'Bumped the plugin version to 0.16.0.0 and configuration schema to 22',
    'Paean remains off for new configurations, upgrades, and reset defaults'
) 'v0.16 Smart Paean, NIN boundary, Guardian sentinel, and schema release notes'
Assert-Literals $normalizedPrivacy @(
    'separate default-off Auto Low-MP Focus helper can set only an empty local native Focus Target',
    'when Auto Low-MP Focus is enabled, the native Focus Target''s empty/occupied state',
    'complete exact canonical `S1`-`S5` set',
    'trusted HP/MP samples and low-MP latches',
    'local identity/text-input state',
    'native 20-yalm range/line-of-sight result for the frozen candidate',
    'when the DRK Shadowbringer macro is enabled, the exact macro line/cycle token',
    'local DRK and current canonical CC target identity or exact native Wolves'' Den striking-dummy hard-target identity',
    'native combo/Shadowbringer recast and queue state',
    'action sequence, animation lock/cast state, HP/Dark Arts and Guard states',
    'both actions'' native range/line-of-sight/readiness results',
    '## Optional Auto Low-MP Focus Target',
    'disabled by default and runs only in exact Crystalline Conflict',
    'complete, unique canonical `S1`-`S5` set',
    'MP must remain trusted at 2,000 or lower for 150 ms',
    'wave clears only after 150 ms continuously at 2,300 MP or higher',
    'Unknown MP never qualifies',
    'native 20-yalm action range and line-of-sight probe',
    'Lowest exact MP ratio wins, then lowest HP ratio, stable S-slot, entity ID, and game-object ID',
    'may invoke exactly one reviewed setter only after Focus was observed stably empty',
    'never clears, replaces, restores, or retries a Focus Target',
    'already occupied Focus spends that low-MP wave without mutation',
    'exact plugin-set readback',
    'confirmed manual or external change or clear latches manual ownership',
    'not a team-visible Attack1 sign and does not change the hard or soft target',
    'no atomic Focus Target compare-and-set',
    'immediately adjacent final empty read followed by an exact readback',
    'live client race remains possible',
    'Nothing is persisted or uploaded',
    '## Experimental DRK Shadowbringer macro helper',
    'disabled by default and runs only for exact PvP Dark Knight in Crystalline Conflict or explicitly enabled Wolves'' Den testing',
    'requires both the existing DRK helper and Wolves'' Den test options; no new setting was added',
    '`/seitonbringer` may arm only the immediately following authored Souleater Combo `<t>` macro line for at most 750 ms',
    'macro name, line cursor, exact local identity/context, proven GCD-cycle token',
    'action/route/mode are kept only long enough to pair those two adjacent lines',
    'In Crystalline Conflict, the target must remain one exact current canonical `S1`-`S5` actor',
    'In Wolves'' Den, the plugin instead reads the local player''s native hard-target ID and the matching object-table battle character',
    'live, targetable combat striking dummy with NameId `541`',
    'freezes and revalidates its game-object ID, entity ID, address, object/sub-kind, NameId, and hard-target ownership',
    'does not query the synthetic `S1`/`<e1>` or native duel-opponent paths for this macro',
    'cannot accept a player, another object, or an alternate target',
    'Frontline and Rival Wings remain excluded',
    'ReAction setup uses both Macro Queue and Turbo',
    'does not create a macro pulse',
    'exact 2.40-second combo recast group restarting with a changed native action sequence',
    'At most one Shadowbringer attempt may be claimed for that cycle',
    'inclusive 0.60-0.80-seconds-remaining window',
    '0.50 seconds or less never triggers Shadowbringer',
    'paired outer Souleater Combo call continues unchanged',
    'Before and after spending the cycle''s one-attempt token',
    'empty stable native queue',
    'clear cast and animation lock',
    'clear own Guard/propagation and target Guard',
    'native 5-yalm combo and 10-yalm Shadowbringer range/line of sight',
    'Base Shadowbringer requires strictly more than 12,000 HP',
    'adjusted Dark Arts action requires the exact Dark Arts status/action state',
    'one normal exact-target Shadowbringer request before the unchanged outer combo call',
    'never changes a hard, soft, or Focus Target',
    'chooses another target/action, replays the macro, or retries',
    'client-accepted return is bounded diagnostic feedback only',
    'does not prove server execution or a clip-free weave',
    'neither persisted nor uploaded',
    'Wolves'' Den dummy result proves only that test path and is not proof of current-patch CC execution or timing',
    'Current English game-data validation independently pins the striking-dummy NameId and the exact per-row combo secondary cost types `0/58/58/147/147/147`',
    'dummy metadata mismatch disables only the Den path',
    'Native GCD sampling starts on the framework update thread rather than performing a local-player lookup during synchronous plugin startup',
    'separate Auto Low-MP Focus Target opt-in',
    'DRK Shadowbringer macro opt-in',
    'Configuration schema 32 is current in v0.30.0.0',
    'Fresh and reset configurations keep NIN Guard-Shukuchi, Smart Recuperate, Hiebsprung, Smart Target/other macro helpers, and all other action-helper masters off',
    'An older explicitly enabled fresh-edge NIN Seiton option still traverses schema 29, migrates to the replacement held-key option',
    'clears the obsolete compatibility field',
    'Every other existing master/helper choice is preserved',
    'post-Guard defaults on only behind the disabled reactive-counter master',
    'Older configurations still traverse the earlier migrations first'
) 'Retained Auto Focus/exact Den-dummy DRK transient-data plus current schema disclosure'
Assert-Literals $normalizedPrivacy @(
    '## Experimental Sage Smart Kardia after accepted Eukrasia',
    'separate persisted option is disabled by default',
    'run only for PvP Sage in exact Crystalline Conflict',
    'does not scan held keys or continuously rank the party while idle',
    'incoming exact PvP Eukrasia `29258` request',
    'records exact local identity, territory, and the Sage''s before-call charge/own-source Eukrasia status evidence',
    'forwards that original request exactly once without changing its action or target',
    'Only a client-accepted Eukrasia request may create a trigger',
    'trigger is valid for at most two seconds',
    'either available Eukrasia charges decreased, or the exact local-source PvP Eukrasia `3107` status appeared when it was previously absent',
    'fresh incoming-party-pressure publication created no earlier than the accepted call',
    'there is no idle Kardia pressure scan and no separate six-second throttle',
    'Candidates under pressure from at least two unique live enemies rank by pressure descending, exact HP ratio ascending, party slot, network entity ID, and game-object ID',
    'exact self is the sole initial fallback',
    'unknown or already-owned Kardion state on the chosen actor ends the opportunity without selecting another actor',
    'Smart Kardia waits for the current animation lock to clear while the trigger remains valid',
    'Before at most one direct-GOID Kardia `29264` request, the trigger and frozen actor are spent',
    'Drift, rejection, or exception cannot rerank, choose a lower candidate, switch to self or another ally, mutate a hard/soft/Focus/mouseover target, replay, or retry',
    'trigger token, timestamps, Eukrasia evidence, pressure publication, frozen actor, result, and aggregate diagnostics remain in memory only',
    'Client acceptance does not prove that Kardia or Kardion applied',
    'current-patch Eukrasia charge/status, animation-lock, dispatch, and reachability behavior remain live-validation boundaries'
) 'v0.20.0.1 accepted-Eukrasia Smart Kardia causal-token, fresh-pressure, direct-target, and persistence disclosure'
Assert-Literals $privacy @(
    '## High-pressure warning, sound, and held Sprint',
    'fixed local top-center warning',
    'narrow work area stacks it below the pressure card if their actual scaled',
    'rectangles would overlap',
    'distinct exact enemies whose current hard target or cast',
    'At least three are required',
    'Recent damage/action',
    'cannot start or sustain the warning, sound, or Sprint eligibility',
    'selected built-in FFXIV UI/system sound once',
    'Unknown/stale pressure hides the visual state immediately but does not rearm a',
    'continuously known below-threshold separation',
    'The separate default-off held-key option can submit an exact ordinary self',
    'Sprint request for a verified high-pressure episode',
    'while physical WASD/arrow',
    'consent remains held',
    'original movement',
    'key is not swallowed',
    'Known unavailable states wait without calling',
    'the native boundary. Only an explicit client rejection may retain the same',
    'frozen episode for the common bounded retry',
    'never changes a selected target, chooses another action',
    'acceptance, ambiguity, or drift is',
    'terminal. It never changes a selected target',
    '## Smart Bard Paean pressure redirect',
    'separate option is disabled by default and exact-Crystalline-Conflict-only',
    'already incoming The Warden''s Paean',
    'ability call `29400`',
    'does not read a generic',
    'physical gameplay-key generation and never creates an action call by itself',
    'complete, unique, stable party view',
    'non-self party member without the live Warden''s Paean ward `3143`',
    'native 30-yalm range and line of sight',
    'trusted count of at least',
    'higher pressure, lower exact HP ratio, party slot, entity ID, and game-object',
    'incoming call are forwarded unchanged',
    'pressure drift suppresses that one call',
    'no cooldown/readiness gate',
    'never changes any selected target or substitutes an',
    'client-accepted return is not stored or',
    'Existing Ally Rescue behavior',
    'unused-marker values `0` and `0xE0000000` are recognized only while the marker',
    '## Experimental Scholar Critical Strategy held-key helper',
    'Scholar in exact Crystalline Conflict',
    'live Guard `3054` or `3673`',
    'native 25-yalm range/line-of-sight result',
    'Pressure is used only for that frozen selection and is not a',
    'Pressure drift neither reranks, switches, nor',
    'No drift can cause another selection, alternate',
    'Configuration schema 32 is current in v0.30.0.0'
) 'Retained pressure escape, Smart Paean, Guardian, Scholar, and current schema local-data/live-boundary disclosure'
Assert-Literals $normalizedPrivacy @(
    'The current action-request priority is **Purify > NIN Seiton > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH Critical Strategy > DRK Hiebsprung > Smart Recuperate > generic Guard > pressure Sprint > event Kardia > event Monk**',
    'One framework frame permits at most one held-helper native boundary',
    'continuously held key remains consent for later distinct exact episodes'
) 'v0.27.1.0 exact action-request priority privacy disclosure'
Assert-Literals $normalizedPrivacy @(
    '## Experimental WHM/BRD/NIN reactive counter-CC',
    'NIN is enabled only when both metadata-verified Forked Raiju `29510` and Fleeting Raiju `29707` rows are available',
    'optional post-Purify path recognizes only exact enemy self-Purify `29056`',
    'also accepts the exact action-level packet when no individual recovery tuple is present',
    'positive live Resilience `3248` is mandatory before the episode can progress',
    'Purify observation remembers the exact actor, local counter action, and event epoch without binding a key',
    'finite, positive, catalog-bounded `RemainingTime` may establish only a non-extending expected end',
    'live status-list membership remains authoritative',
    'first real absent frame at or after that end is eligible immediately',
    'early or untimed absence still requires 150 ms of continuous proof',
    'Purify and Guard remember the exact enemy episode while protection is live',
    'Authoritative absence opens the original strict 500-ms acquisition edge',
    'only a current eligible generation acquired inside it may bind',
    'dispatches directly to the frozen actor without requiring or changing the selected target',
    'There is no minimum team-pressure count',
    'at most one active post-Purify state per canonical `S1`-`S5` slot plus a bounded deduplication set',
    'at most five already- deduplicated signals may retain only their original caster/event identity until the original 750-ms acquisition deadline',
    'They carry no key or action, cannot fall back to another actor, and are retired on context, local identity/job, or feature-generation change',
    'optional post-Guard path observes only exact Guard `3054` or `3673` present on one live canonical `S1`-`S5` enemy',
    'Its first exact presence remembers the actor, local action, Guard epoch, and the same kind of bounded non-extending duration hint without binding a key',
    'first verified framework observation that finds Guard absent',
    'including an early manual Guard cancel',
    'first verified framework observation that finds Guard absent exposes one strict, non-extending 500-ms key-acquisition opportunity',
    'Only a current eligible held/fresh generation acquired inside that original edge can bind the episode',
    'After binding, key release retires that intent',
    'with no minimum team-pressure count',
    'Only a fresh exact pressure count above zero earns a ranking bonus, with higher positive counts first',
    'Known zero, unknown, or stale pressure is neutral and never excludes or delays a candidate',
    'Lower HP ratio follows, then lower trusted MP ratio and stable canonical slot/identity',
    'Exactly one winner is selected',
    'Every simultaneous loser is terminal and cannot become a fallback attempt',
    'selection occurs once an exact key is acquired inside the original 500-ms protection-end edge',
    'Guard retires every simultaneous loser before a higher-priority wait',
    'no later rank change or alternate can replace the winner',
    'One continuous hold can authorize later distinct startup or protection-end episodes',
    'each selected episode remains one frozen intent and no simultaneous loser can follow it',
    'PvP Spinning Edge/Aeolian Edge Combo carrier `29500`',
    'metadata-verified Forked Raiju `29510` or Fleeting Raiju `29707`',
    'standard Purify-removable protection matrix',
    'Sealed Forked Raiju status `3195` to be absent',
    'both variants require exact local Bind `1345` to be absent',
    'urgent startup may bind the first current eligible held/fresh generation only inside that startup''s original short event lease',
    'Expired or disabled leases retire, and its exact local job, action, and enemy are revalidated before new packets are compared',
    'later exact urgent startup may replace only an unattempted lower-priority reactive lease',
    'Once bound, a later key cannot inherit that episode',
    'text input poisons the exact generation until real release',
    'uses only the common bounded same-intent retry',
    'never requires or switches the selected target, chooses an alternate action/actor, or replays input',
    'Only a clean native rejection may retain the same frozen intent under the shared bounded retry policy',
    'WHM uses only Wunder der Natur / Miracle of Nature `29228`',
    'BRD uses only Stumme Nocturne / Silent Nocturne `29395`',
    'Stun `1343` for either exact NIN Raiju variant',
    'Every bound post-Purify/post-Guard action uses the same three-second protection-end deadline measured from its original release',
    'An unbound opportunity still expires at the strict 500-ms acquisition boundary',
    'same nonzero source sequence created by the plugin''s accepted native request',
    'A manual use with a different sequence cannot claim the pending automatic result',
    'key acquisition, waiting, and retry never restart or extend that deadline'
) 'Current post-Purify/post-Guard shared lease, actor/key freeze, exact confirmation, and no-target-mutation disclosure'

$configurationPath = Join-Path $sourceRoot 'SeitonSense.Plugin\Models\PluginConfiguration.cs'
$configuration = Read-RequiredSource $configurationPath 'Plugin configuration'
$normalizedConfiguration = $configuration -replace '\s+', ' '
Assert-Literals $configuration @(
    'public int Version { get; set; } = 32',
    'public bool PurifyOnHeldGameplayKey { get; set; }',
    'if (Version < 6)',
    'PurifyOnHeldGameplayKey = false',
    'if (Version < 7)',
    'ApplyFocusGlowDefaults(false)',
    'ApplyCurrentTargetHighlightDefaults(false)',
    'ShowCurrentTargetInfoHud = false',
    'if (Version < 8)',
    'EnableNearAssistMacro = false',
    'NearAssistMaxAllyDistance = 25f',
    'NearAssistPreferDamageRoles = true',
    'if (Version < 9)',
    'WarnMarksmanSpite = true',
    'if (Version < 10)',
    'NearAssistPreferTeamPressure = false',
    'ShowPressureCounter = true',
    'ShowIncomingPressureOnNameplates = true',
    'ShowTeamPressureOnNameplates = true',
    'ShowCcProtection = true',
    'MchLimitBreakSoundEnabled = true',
    'MchLimitBreakSoundId = 6',
    'if (Version < 11)',
    'CcProtectionEmblemScale = 1f',
    'if (Version < 12)',
    'ExperimentalAllyRescueOnNextKey = false',
    'AllyRescueOnHeldGameplayKey = false',
    'if (Version < 13)',
    'ExperimentalMiracleInterceptOnHeldKey = false',
    'MiracleInterceptMchLimitBreak = true',
    'MiracleInterceptSamZantetsuken = true',
    'MiracleInterceptViperNest = true',
    'if (Version < 14)',
    'EnableResourceAura = true',
    'ResourceAuraOnSelfHotbars = true',
    'ResourceAuraOnPartyRows = true',
    'ResourceAuraOnCcTeamRows = true',
    'ResourceAuraHpPercent = 30',
    'ResourceAuraMpThreshold = 2000',
    'ResourceAuraIntensity = 0.8f',
    'ResourceAuraPulseSpeed = 0.75f',
    'EnableMonkEarthReplyHelper = false',
    'MonkEarthReplyOnLowHp = true',
    'MonkEarthReplyBeforeExpiry = true',
    'MonkEarthReplyHpPercent = 30',
    'MonkEarthReplyExpirySeconds = 1.25f',
    'if (Version < 15)',
    'EnableCcImmunityBrake = false',
    'CcBrakeJobs = CreateDefaultCcBrakeJobs()',
    'CcBrakeActions = CreateDefaultCcBrakeActions()',
    'if (Version < 16)',
    'MiracleInterceptAfterPurifiedStun = false',
    'if (Version < 17)',
    'EnableDefensiveUtilities = false',
    'DefensiveUtilitiesOnHeldKey = true',
    'GuardOnStunPressure = true',
    'PreGuardOnLowHpPressure = true',
    'PaladinGuardianLowAlly = true',
    'EnableReactiveCcUtilities = ExperimentalMiracleInterceptOnHeldKey',
    'ReactiveCcOnHeldKey = true',
    'ReactiveCcDancerLimitBreak = false',
    'ReactiveCcAfterEnemyPurify = MiracleInterceptAfterPurifiedStun',
    'if (Version < 18)',
    'NearHelpPreferIncomingPressure = true',
    'if (Version < 19)',
    'EnableNinjaSeitonOnFreshGameplayKey = false',
    'public bool EnableNinjaSeitonOnHeldGameplayKey { get; set; }',
    'if (Version < 20)',
    'PaladinGuardianAnnounceAndMark = false',
    'if (Version < 21)',
    'EnableScholarCriticalStrategyOnHeldKey = false',
    'if (Version < 22)',
    'EnableBardWardensPaeanPressureRedirect = false',
    'if (Version < 23)',
    'ShowHighPressureWarning = false',
    'PlayHighPressureWarningSound = false',
    'HighPressureWarningSoundId = 6',
    'EnablePressureEscapeSprintOnHeldKey = false',
    'WarnWhenIsolated = true',
    'IsolationWarningScale = 1f',
    'EnableAutoEnemyFocusMark = false',
    'if (Version < 24)',
    'public bool EnableAutoLowMpFocusTarget { get; set; }',
    'public bool EnableDarkKnightShadowbringerMacro { get; set; }',
    'EnableAutoLowMpFocusTarget = false',
    'EnableDarkKnightShadowbringerMacro = false',
    'if (Version < 25)',
    'public bool EnableSageKardiaOnHeldKey { get; set; }',
    'EnableSageKardiaOnHeldKey = false',
    'if (Version < 26)',
    'public bool EnableSageKardiaAfterEukrasia { get; set; }',
    'public bool EnableSmartRecuperateOnHeldKey { get; set; }',
    'public bool EnableNinjaGuardShukuchiOnHeldGameplayKey { get; set; }',
    'public bool AllowHeldHelpersToCancelOwnCast { get; set; }',
    'public bool PaladinGuardianOnHeldKey { get; set; } = true',
    'var guardianWasEnabled = EnableDefensiveUtilities && PaladinGuardianLowAlly;',
    'PaladinGuardianLowAlly = guardianWasEnabled;',
    'PaladinGuardianOnHeldKey = DefensiveUtilitiesOnHeldKey;',
    'EnableSageKardiaAfterEukrasia = EnableSageKardiaOnHeldKey;',
    'EnableSmartRecuperateOnHeldKey = false;',
    'PreGuardOnLowHpPressure = false;',
    'public bool ShowCombatFrames { get; set; }',
    'public bool EnableDarkKnightPlungeOnHeldKey { get; set; }',
    'public bool CombatFramesEnableInteraction { get; set; } = true;',
    'public bool CombatFramesShowLimitBreaks { get; set; } = true;',
    'public bool ShowAllyLimitBreakDamageEvents { get; set; } = true;',
    'public bool CombatFramesShowNames { get; set; } = true;',
    'ShowCombatFrames = false;',
    'if (Version < 27)',
    'EnableDarkKnightPlungeOnHeldKey = false;',
    'CombatFramesEnableInteraction = false;',
    'CombatFramesShowLimitBreaks = true;',
    'ShowAllyLimitBreakDamageEvents = true;',
    'if (Version < 28)',
    'ReactiveCcAfterEnemyGuard = false;',
    'if (Version < 29)',
    'EnableNinjaSeitonOnHeldGameplayKey = EnableNinjaSeitonOnFreshGameplayKey;',
    'if (Version < 30)',
    'AllowHeldHelpersToCancelOwnCast = false;',
    'if (Version < 31)',
    'EnableNinjaGuardShukuchiOnHeldGameplayKey = false;',
    'if (Version < 32)',
    'ShowCombatFrames = false;',
    'ShowEnemyLimitBreaksOnNameplates = true;',
    'ShowLimitBreakActivationMessages = true;',
    'LimitBreakFeedShowNames = CombatFramesShowNames;',
    'ShowAllyLimitBreakDamageEvents = true;',
    'PlayLocalMpWarningSounds = true;',
    'LocalMpWarning4000SoundId = 4;',
    'LocalMpWarning2000SoundId = 6;',
    'Version = 32',
    'ApplyCombatFramesLayoutDefaults()',
    'ApplyCombatFramesCleanPreset()',
    'NormalizeCcBrakeSelections()',
    'IsCcBrakeJobEnabled(uint jobId)',
    'IsCcBrakeActionEnabled(uint actionId)',
    'if (actionId is 29244 or 29248)',
    'normalizedActions[29248] = gravityEnabled',
    'Math.Clamp(NearAssistMaxAllyDistance, 5f, 30f)',
    'Math.Clamp(MchLimitBreakSoundId, 1, 16)',
    'Math.Clamp(HighPressureWarningSoundId, 1, 16)',
    'Clamp(IsolationWarningScale, 0.75f, 1.75f, 1f',
    'Clamp(CcProtectionEmblemScale, 0.75f, 1.75f, 1f',
    'Clamp(ResourceAuraIntensity, 0.1f, 1.5f, 0.8f',
    'Clamp(ResourceAuraPulseSpeed, 0.2f, 2f, 0.75f',
    'Math.Clamp(ResourceAuraHpPercent, 10, 80)',
    'Math.Clamp(ResourceAuraMpThreshold, 0, 10_000)',
    'Clamp(CombatFramesEnemyScreenX, 0.02f, 0.98f, 0.82f',
    'Clamp(CombatFramesEnemyScreenY, 0.02f, 0.98f, 0.48f',
    'Clamp(CombatFramesSelfScreenX, 0.02f, 0.98f, 0.5f',
    'Clamp(CombatFramesSelfScreenY, 0.02f, 0.98f, 0.78f',
    'Clamp(CombatFramesScale, 0.55f, 1.8f, 1f',
    'Clamp(CombatFramesBackgroundOpacity, 0.35f, 1f, 0.92f',
    'Math.Clamp(MonkEarthReplyHpPercent, 10, 80)',
    'MonkEarthReplyExpirySeconds,',
    '0.5f,',
    '2.5f,'
) 'Schema-32 replacement LB/MP defaults plus retained default-off action helpers and legacy Combat Frames compatibility fields'
if ($configuration -notmatch '(?m)^\s*public bool EnableDefensiveUtilities \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool DefensiveUtilitiesOnHeldKey \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool GuardOnStunPressure \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool PreGuardOnLowHpPressure \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool PaladinGuardianLowAlly \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool PaladinGuardianOnHeldKey \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool PaladinGuardianAnnounceAndMark \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableScholarCriticalStrategyOnHeldKey \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableBardWardensPaeanPressureRedirect \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ShowHighPressureWarning \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool PlayHighPressureWarningSound \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public int HighPressureWarningSoundId \{ get; set; \} = 6;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnablePressureEscapeSprintOnHeldKey \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableReactiveCcUtilities \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ReactiveCcOnHeldKey \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ReactiveCcDancerLimitBreak \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ReactiveCcAfterEnemyPurify \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ReactiveCcAfterEnemyGuard \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool WarnWhenIsolated \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableAutoEnemyFocusMark \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableDarkKnightPlungeOnHeldKey \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool CombatFramesEnableInteraction \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool CombatFramesShowLimitBreaks \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ShowAllyLimitBreakDamageEvents \{ get; set; \} = true;\s*$') {
    throw 'Schema 28 new installations must keep action/marker/Combat Frames masters off, preconfigure the post-Guard leaf behind the disabled reactive master, remove speculative pre-Guard, and retain reviewed held/reactive/isolation plus behind-master interaction/LB leaf defaults.'
}
if ([regex]::Matches($configuration, '\bEnableDefensiveUtilities\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bEnableReactiveCcUtilities\s*=\s*false\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bEnableAutoEnemyFocusMark\s*=\s*false\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bPaladinGuardianAnnounceAndMark\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bEnableScholarCriticalStrategyOnHeldKey\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bEnableSageKardiaOnHeldKey\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bEnableBardWardensPaeanPressureRedirect\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bShowHighPressureWarning\s*=\s*true\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bShowHighPressureWarning\s*=\s*false\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bPlayHighPressureWarningSound\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bEnablePressureEscapeSprintOnHeldKey\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bEnableDarkKnightPlungeOnHeldKey\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bCombatFramesEnableInteraction\s*=\s*false\s*;').Count -ne 1 -or
    [regex]::Matches($configuration, '\bCombatFramesEnableInteraction\s*=\s*true\s*;').Count -ne 1 -or
    [regex]::Matches($configuration, '\bCombatFramesShowLimitBreaks\s*=\s*true\s*;').Count -ne 2 -or
    [regex]::Matches($configuration, '\bShowAllyLimitBreakDamageEvents\s*=\s*true\s*;').Count -ne 3 -or
    [regex]::Matches($configuration, '\bWarnWhenIsolated\s*=\s*true\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bDefensiveUtilitiesOnHeldKey\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bReactiveCcOnHeldKey\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bReactiveCcAfterEnemyGuard\s*=\s*true\s*;').Count -ne 1 -or
    [regex]::Matches($configuration, '\bReactiveCcAfterEnemyGuard\s*=\s*false\s*;').Count -ne 1) {
    throw 'Schema migrations/reset defaults must preserve opt-in action/marker/Sprint/sound/DRK-Plunge masters, visible-by-default isolation, fresh/reset-only high-pressure visuals, retired interaction, and replacement LB defaults.'
}
if ($configuration -notmatch '(?m)^\s*public bool NearHelpPreferIncomingPressure \{ get; set; \} = true;\s*$' -or
    [regex]::Matches($configuration, '\bNearHelpPreferIncomingPressure\s*=\s*true\s*;').Count -lt 2) {
    throw 'Schema 18 must enable the bounded Near Help pressure preference for upgrades and reset defaults while the shared helper master remains opt-in.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableAutoLowMpFocusTarget \{ get; set; \}\s*$' -or
    [regex]::Matches($configuration, '\bEnableAutoLowMpFocusTarget\s*=\s*false\s*;').Count -lt 2 -or
    $configuration -match '(?m)^\s*public bool EnableAutoLowMpFocusTarget \{ get; set; \}\s*=\s*true;') {
    throw 'Schema 24 must keep Auto Low-MP Focus off for upgrades and ResetToDefaults, with a plain default-false property.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableDarkKnightShadowbringerMacro \{ get; set; \}\s*$' -or
    [regex]::Matches($configuration, '\bEnableDarkKnightShadowbringerMacro\s*=\s*false\s*;').Count -lt 2 -or
    $configuration -match '(?m)^\s*public bool EnableDarkKnightShadowbringerMacro \{ get; set; \}\s*=\s*true;') {
    throw 'Schema 24 must keep the action-initiating DRK macro off for upgrades and ResetToDefaults, with a plain default-false property.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableSageKardiaOnHeldKey \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableSageKardiaAfterEukrasia \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableSmartRecuperateOnHeldKey \{ get; set; \}\s*$' -or
    [regex]::Matches($configuration, '\bEnableSageKardiaOnHeldKey\s*=\s*false\s*;').Count -lt 3 -or
    [regex]::Matches($configuration, '\bEnableSageKardiaAfterEukrasia\s*=\s*false\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bEnableSmartRecuperateOnHeldKey\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bPreGuardOnLowHpPressure\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bShowCombatFrames\s*=\s*false\s*;').Count -lt 2 -or
    $configuration -match '(?m)^\s*public bool (?:EnableSageKardiaOnHeldKey|EnableSageKardiaAfterEukrasia|EnableSmartRecuperateOnHeldKey|ShowCombatFrames) \{ get; set; \}\s*=\s*true;') {
    throw 'Schema 27 must retain schema-26 Smart Recuperate and Combat Frames default-off behavior, retire held Kardia, and keep accepted-Eukrasia Kardia off except for the explicit one-time schema-25 opt-in migration.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableNinjaSeitonOnFreshGameplayKey \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableNinjaSeitonOnHeldGameplayKey \{ get; set; \}\s*$' -or
    [regex]::Matches($configuration, '\bEnableNinjaSeitonOnFreshGameplayKey\s*=\s*false\s*;').Count -lt 3 -or
    [regex]::Matches($configuration, '\bEnableNinjaSeitonOnHeldGameplayKey\s*=\s*false\s*;').Count -ne 1 -or
    [regex]::Matches($configuration, '\bEnableNinjaSeitonOnHeldGameplayKey\s*=\s*EnableNinjaSeitonOnFreshGameplayKey\s*;').Count -ne 1) {
    throw 'Schema 29 must migrate only an explicit legacy NIN fresh-edge opt-in to held consent, clear the compatibility field, and keep fresh/reset NIN automation off.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableNinjaGuardShukuchiOnHeldGameplayKey \{ get; set; \}\s*$' -or
    [regex]::Matches($configuration, '\bEnableNinjaGuardShukuchiOnHeldGameplayKey\s*=\s*false\s*;').Count -lt 2 -or
    $configuration -match '(?m)^\s*public bool EnableNinjaGuardShukuchiOnHeldGameplayKey \{ get; set; \}\s*=\s*true;') {
    throw 'Schema 31 must keep the target-mutating NIN Guard-Shukuchi helper off for upgrades and ResetToDefaults, with a plain default-false property.'
}
if ([regex]::Matches($configuration, '\bVersion\s*=\s*32\s*;').Count -lt 2 -or
    $normalizedConfiguration -notmatch 'if \(Version >= 32\).*?return;.*?if \(Version < 29\).*?EnableNinjaSeitonOnHeldGameplayKey = EnableNinjaSeitonOnFreshGameplayKey;.*?EnableNinjaSeitonOnFreshGameplayKey = false;.*?if \(Version < 30\).*?AllowHeldHelpersToCancelOwnCast = false;.*?if \(Version < 31\).*?EnableNinjaGuardShukuchiOnHeldGameplayKey = false;.*?if \(Version < 32\).*?ShowCombatFrames = false;.*?ShowEnemyLimitBreaksOnNameplates = true;.*?ShowLimitBreakActivationMessages = true;.*?LimitBreakFeedShowNames = CombatFramesShowNames;.*?ShowAllyLimitBreakDamageEvents = true;.*?PlayLocalMpWarningSounds = true;.*?LocalMpWarning4000SoundId = 4;.*?LocalMpWarning2000SoundId = 6;.*?Version = 32;') {
    throw 'Schema 32 must fast-path current settings, preserve the explicit legacy NIN opt-in mapping, keep cast cancellation and Guard-Shukuchi opt-in, retire Combat Frames, and initialize the replacement LB and local-MP presentation defaults.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableCcImmunityBrake \{ get; set; \}\s*$' -or
    [regex]::Matches($configuration, '\bEnableCcImmunityBrake\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bCcBrakeJobs\s*=\s*CreateDefaultCcBrakeJobs\(\)\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bCcBrakeActions\s*=\s*CreateDefaultCcBrakeActions\(\)\s*;').Count -lt 2) {
    throw 'Schema 15 must keep the CC-immunity brake master default-off while job/action selections default on for explicit opt-in.'
}
$configuredBrakeJobs = [regex]::Match(
    $configuration,
    '(?s)SupportedCcBrakeJobIds\s*=\s*\[(?<Body>.*?)\];')
$configuredBrakeActions = [regex]::Match(
    $configuration,
    '(?s)SupportedCcBrakeActionIds\s*=\s*\[(?<Body>.*?)\];')
$configuredBrakeJobIds = @([regex]::Matches($configuredBrakeJobs.Groups['Body'].Value, '(?m)^\s*(?<Id>\d+)\s*,') | ForEach-Object { [uint32]$_.Groups['Id'].Value })
$configuredBrakeActionIds = @([regex]::Matches($configuredBrakeActions.Groups['Body'].Value, '(?m)^\s*(?<Id>\d+)\s*,') | ForEach-Object { [uint32]$_.Groups['Id'].Value })
$catalogBrakeActionIds = @($ccBrakeDefinitions | ForEach-Object { [uint32](($_.Groups['Action'].Value) -replace '_', '') })
if (-not $configuredBrakeJobs.Success -or -not $configuredBrakeActions.Success -or
    ($configuredBrakeJobIds -join ',') -ne ($expectedCcBrakeJobs -join ',') -or
    ($configuredBrakeActionIds -join ',') -ne ($catalogBrakeActionIds -join ',')) {
    throw 'Schema 15 job/action toggle allowlists must exactly match the reviewed nine-job, twelve-action brake catalog.'
}
if ($configuration -notmatch '(?s)CreateDefaultCcBrakeJobs\(\).*?SupportedCcBrakeJobIds\.ToDictionary\(static id => id, static _ => true\)' -or
    $configuration -notmatch '(?s)CreateDefaultCcBrakeActions\(\).*?SupportedCcBrakeActionIds\.ToDictionary\(static id => id, static _ => true\)' -or
    $configuration -notmatch '(?s)if \(actionId is 29244 or 29248\).*?CcBrakeActions\[29244\] = enabled;.*?CcBrakeActions\[29248\] = enabled;' -or
    $configuration -notmatch '(?s)var gravityEnabled = normalizedActions\[29244\];.*?normalizedActions\[29248\] = gravityEnabled;') {
    throw 'Brake selections must default every reviewed leaf on and keep both adjusted AST Gravity II forms behind one setting.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableResourceAura \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ResourceAuraOnSelfHotbars \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ResourceAuraOnPartyRows \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ResourceAuraOnCcTeamRows \{ get; set; \} = true;\s*$' -or
    [regex]::Matches($configuration, '\bEnableResourceAura\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bResourceAuraOnSelfHotbars\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bResourceAuraOnPartyRows\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bResourceAuraOnCcTeamRows\s*=\s*true\s*;').Count -lt 2) {
    throw 'Resource aura and its three surfaces must be visible-by-default for new, migrated, and reset schema-14 configurations.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableMonkEarthReplyHelper \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool MonkEarthReplyOnLowHp \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool MonkEarthReplyBeforeExpiry \{ get; set; \} = true;\s*$' -or
    [regex]::Matches($configuration, '\bEnableMonkEarthReplyHelper\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bMonkEarthReplyHpPercent\s*=\s*30\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bMonkEarthReplyExpirySeconds\s*=\s*1\.25f\s*;').Count -lt 2) {
    throw 'Monk Earth Reply must stay master-default-off with low-HP and expiry trigger defaults set to 30 percent and 1.25 seconds.'
}

$guardRules = Read-RequiredSource (Join-Path $coreRoot 'GuardCooldownRules.cs') 'Guard cooldown rules'
$mpRules = Read-RequiredSource (Join-Path $coreRoot 'LowMpRules.cs') 'Low-MP rules'
foreach ($pair in @(
    @($combatConstants, 'GuardDurationSeconds = 4f'),
    @($combatConstants, 'GuardCooldownSeconds = 30f'),
    @($combatConstants, 'RecuperateMpCost = 2000'),
    @($combatConstants, 'LowMpExitThreshold = 2300'),
    @($combatConstants, 'PurifyMpCost = 2000'),
    @($guardRules, 'CooldownMilliseconds = 30_000'),
    @($guardRules, 'ActiveDurationMilliseconds = 4_000'),
    @($mpRules, 'RecuperateCost = 2_000'),
    @($mpRules, 'ExitThreshold = 2_300'))) {
    if ($pair[0] -notmatch [regex]::Escape($pair[1])) {
        throw "Core/runtime combat constants drifted: $($pair[1])"
    }
}

Write-Host "Seiton Sense v0.30.0.0 safety contract verified across $($sourceFiles.Count) source files with schema 32 and the exact 388-test Core registry. The custom-repository listing is visible (IsHide false), and the deleted Combat Frames renderer, targeting/mouseover, snapshot, Settings, and calibrated-gauge runtime files remain absent. Smart Target is one-shot, exact-action/actor, pressure-aware, fallback-only, and fail-closed; local MP sounds plus enemy-nameplate/self/ally LB surfaces remain local and action-free. Auto-Seiton remains a persisted toggle gated by held-key consent. Guardian keeps unconditional <=20% rescue and admits 21-35% only under fresh exact 3+ pressure. Accepted plugin Auto-Guard owns cancellation protection only through both central UseAction and UseActionLocation hooks, with exact PvP metadata, raw/resolved Guard and scoped /panicshu release, 1.5-second propagation, exact live-status follow-through, and a hard six-second fail-open cap. Runtime held-helper priority remains Purify > NIN Seiton > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > SCH Critical Strategy > DRK Hiebsprung > Smart Recuperate > generic Guard > pressure Sprint > event Kardia > event Monk."
