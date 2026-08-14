param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$sourceRoot = Join-Path $resolvedRoot 'src'
$pluginServicesRoot = Join-Path $sourceRoot 'SeitonSense.Plugin\Services'
$pluginUiRoot = Join-Path $sourceRoot 'SeitonSense.Plugin\UI'
$coreRoot = Join-Path $sourceRoot 'SeitonSense.Core'
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
if ($sourceFiles.Count -eq 0) { throw 'No C# source files found.' }

function Read-RequiredSource([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label source is missing: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw
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
    'target mutation services' = '(?-i:\bTargetManager\b)|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*='
    'native UI or input injection' = '\b(SendInput|keybd_event|mouse_event|ExecuteCommand|SetRawValue|ClearAll|FireCallback|SendEvent)\b'
    'gameplay file writes' = '\b(File\.Write|FileStream|StreamWriter|Directory\.CreateDirectory)\b'
    'native UI mutation' = '\b(LoadIconTexture|UnloadTexture|ToggleVisibility|SetPosition|SetScale|SetAlpha|SetAdditive|SetMultiply|SetColor|Destroy|PulseActionBarSlot)\s*\('
}

foreach ($check in $forbiddenChecks.GetEnumerator()) {
    $matches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern $check.Value)
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
$allyRescueProbePath = Join-Path $pluginServicesRoot 'AllyRescueProbe.cs'
$miracleInterceptProbePath = Join-Path $pluginServicesRoot 'MiracleInterceptProbe.cs'
$monkEarthReplyProbePath = Join-Path $pluginServicesRoot 'MonkEarthReplyProbe.cs'
$resourceAuraAnchorPath = Join-Path $pluginServicesRoot 'ResourceAuraAnchorTracker.cs'
$allyRescueConfirmationRulesPath = Join-Path $coreRoot 'AllyRescueConfirmationRules.cs'
$miracleCleanseFollowupRulesPath = Join-Path $coreRoot 'MiracleCleanseFollowupRules.cs'
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
$allowedUnsafe = @(
    $slotResolverPath,
    $readinessPath,
    $namePlateAnchorPath,
    $inputContextPath,
    $purifyProbePath,
    $allyRescueProbePath,
    $miracleInterceptProbePath,
    $monkEarthReplyProbePath,
    $resourceAuraAnchorPath,
    $nearAssistPath,
    $partySlotResolverPath,
    $machinistLimitBreakCapturePath,
    $machinistLimitBreakWarningSoundPath,
    $targetPressureTrackerPath,
    $ccImmunityBrakeServicePath
)

$unsafeMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bunsafe\b')
$unexpectedUnsafe = @($unsafeMatches | Where-Object { $allowedUnsafe -notcontains $_.Path })
if ($unexpectedUnsafe.Count -gt 0) {
    $locations = $unexpectedUnsafe | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Unsafe code is allowed only in the reviewed native boundaries: $($locations -join ', ')"
}

# Near Assist, Near Help, and Far Help share one target-only action detour. The MCH/pressure capture owns one
# read-only ActionEffect receive hook. Plugin.cs only constructor-injects interop.
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
foreach ($allowed in $allowedUnsafe) {
    if (-not (Test-Path -LiteralPath $allowed -PathType Leaf)) {
        throw "Expected narrow probe is missing: $allowed"
    }
    if (-not ($unsafeMatches.Path -contains $allowed)) {
        throw "Expected narrow probe contains no explicit unsafe boundary: $allowed"
    }
}

# Target highlighting may read the current and focus targets in one dedicated renderer.
# No other feature may acquire ITargetManager, and no target setter is permitted anywhere.
$targetManagerMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\bITargetManager\b')
$unexpectedTargetManager = @($targetManagerMatches | Where-Object {
    $_.Path -notin @($pluginPath, $targetHighlightPath)
})
if ($unexpectedTargetManager.Count -gt 0) {
    $locations = $unexpectedTargetManager | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "ITargetManager is allowed only for constructor injection and the dedicated read-only renderer: $($locations -join ', ')"
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
    $targetHighlight -match '\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=' -or
    $targetHighlight -match '\b(INamePlateGui|NamePlateAnchorTracker|NamePlateObject|NameIcon)\b') {
    throw 'Target highlighting must remain read-only and separate from native nameplates and existing icon slots.'
}
if ($targetHighlight -match '(?m)^\s*private\s+(?:readonly\s+)?IGameObject\??\s+') {
    throw 'Target wrappers must be resolved and discarded within the current draw frame.'
}

# Action initiation remains globally forbidden except for one exact self-Purify,
# one exact job-gated ally-rescue, one exact WHM Miracle intercept, and one exact
# default-off Monk Earth's Reply call. Near
# Assist/Near Help/Far Help may only forward an incoming action through their sole Original.
$actionMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(UseAction|UseActionLocation|ExecuteAction|SendAction)\b')
$unexpectedAction = @($actionMatches | Where-Object {
    $reviewedActionBoundary =
        $_.Path -in @($purifyProbePath, $allyRescueProbePath, $miracleInterceptProbePath, $monkEarthReplyProbePath, $nearAssistPath) -and
        $_.Line -match '\bUseAction\b'
    $reviewedBrakeDocumentation =
        $_.Path -eq $ccImmunityBrakeTargetRulesPath -and
        $_.Line -match '^\s*///.*\bUseAction\b'
    -not ($reviewedActionBoundary -or $reviewedBrakeDocumentation)
})
if ($unexpectedAction.Count -gt 0) {
    $locations = $unexpectedAction | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only EmergencyPurifyProbe, AllyRescueProbe, MiracleInterceptProbe, MonkEarthReplyProbe, and the bounded shared macro detour may reference UseAction: $($locations -join ', ')"
}

# Warning audio is restricted to one bounded client-owned chat sound. External audio
# libraries, audio-file reads, URLs, and any second native sound path fail the build.
$warningSound = Read-RequiredSource $machinistLimitBreakWarningSoundPath 'Machinist limit-break warning sound'
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
) 'Bounded MCH warning sound'
if ([regex]::Matches($warningSound, '\bUIGlobals\.PlayChatSoundEffect\s*\(').Count -ne 1) {
    throw 'MCH warning sound must contain exactly one client-owned PlayChatSoundEffect call.'
}
$consumeThreatToken = [regex]::Match($warningSound, '\blastThreatToken\s*=\s*threatToken\s*;')
$playThreatSound = [regex]::Match($warningSound, '\breturn\s+TryPlay\s*\(\s*soundId\s*\)\s*;')
if (-not $consumeThreatToken.Success -or -not $playThreatSound.Success -or
    $consumeThreatToken.Index -gt $playThreatSound.Index) {
    throw 'MCH threat sound must consume its one-shot token before the native sound request.'
}
if ($warningSound -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event)\b') {
    throw 'MCH warning audio must never initiate actions or mutate input/targets.'
}
$soundApiMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(UIGlobals\.PlayChatSoundEffect|SoundPlayer|MediaPlayer|PlaySound|sndPlaySound|NAudio|FMOD|XAudio2|AudioClient|WaveOut|WasapiOut)\b')
$unexpectedSoundApis = @($soundApiMatches | Where-Object {
    $_.Path -ne $machinistLimitBreakWarningSoundPath -or
    $_.Line -notmatch '\bUIGlobals\.PlayChatSoundEffect\s*\(\s*\(uint\)soundId\s*\)'
})
if ($unexpectedSoundApis.Count -gt 0) {
    $locations = $unexpectedSoundApis | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only the exact client-owned MCH chat sound call is permitted: $($locations -join ', ')"
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
    'EnemyCombatConstants.PurifyActionId',
    'MiracleInterceptRules.ClassifyExactStartSignal(',
    'MiracleCleanseFollowupRules.IsExactStunPurifySignal(',
    'IsEmpty(targetEffects[0])',
    'HasOnlyEmptyAdditionalEffects(targetEffects)',
    'if (depth > MaximumQueuedMiracleInterceptThreats)',
    'DroppedMiracleInterceptThreats',
    'TryCaptureMiracleInterceptConfirmation',
    'MiracleInterceptConfirmationRules.MiracleOfNatureActionId',
    'MiracleInterceptConfirmationRules.AddStatusEffectType',
    'MiracleInterceptConfirmationRules.MiracleOfNatureStatusId',
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
$normalizedMchCapture = $mchCapture -replace '\s+', ' '
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
if ($normalizedMiracleConfirmationCapture -notmatch 'casterEntityId != localEntityId.*?header->NumTargets != 1.*?actionId != MiracleInterceptConfirmationRules\.MiracleOfNatureActionId.*?targetEntityId == localEntityId.*?for \(var slot = 0; slot < EffectSlotsPerTarget; slot\+\+\).*?effect\.Type != MiracleInterceptConfirmationRules\.AddStatusEffectType \|\| effect\.Value != MiracleInterceptConfirmationRules\.MiracleOfNatureStatusId.*?return new MiracleInterceptLandedEffect' -or
    $normalizedMiracleConfirmationCapture -notmatch 'confirmation\.CasterEntityId != CurrentMiracleInterceptLocalEntityId.*?!IsNetworkEntityId\(confirmation\.TargetEntityId\).*?if \(depth > MaximumQueuedMiracleInterceptConfirmations\).*?pendingMiracleInterceptConfirmations\.Enqueue\(confirmation\)') {
    throw 'Miracle landing capture must require the exact local caster, one non-self network target, action 29228, and AddStatus 0x0E/value 3085 before bounded enqueue.'
}
if ($normalizedMchCapture -notmatch 'MiracleInterceptRules\.ClassifyExactStartSignal\( actionId, casterEntityId, targetEntityId, header->NumTargets, targetEffects\[0\]\.Type, IsEmpty\(targetEffects\[0\]\), HasOnlyEmptyAdditionalEffects\(targetEffects\)\)' -or
    $normalizedMchCapture -notmatch 'for \(var index = 1; index < effects\.Length; index\+\+\).*?!IsEmpty\(effects\[index\]\)') {
    throw 'Miracle threat capture must pass the exact single-target identity and all eight effect-slot facts to the pure classifier.'
}

if ($normalizedMchCapture -notmatch 'var localEntityId = actionId == EnemyCombatConstants\.PurifyActionId \? CurrentMiracleCleanseFollowupLocalEntityId : CurrentMiracleInterceptLocalEntityId; if \(!IsNetworkEntityId\(localEntityId\) \|\| casterEntityId == localEntityId\) return null;' -or
    $normalizedMchCapture -notmatch 'public void SetMiracleCleanseFollowupLocalEntityId\(uint entityId\).*?ref miracleCleanseFollowupLocalEntityIdBits.*?if \(previous != normalized\) Interlocked\.Increment\(ref miracleCleanseFollowupGeneration\)') {
    throw 'Post-Purify Stun capture must use its own opt-in local-identity gate and generation, independently from ordinary Miracle capture.'
}
if ($normalizedMchCapture -notmatch 'if \(actionId == EnemyCombatConstants\.PurifyActionId\) \{ for \(var slot = 0; slot < EffectSlotsPerTarget; slot\+\+\) \{ var effect = targetEffects\[slot\]; if \(!MiracleCleanseFollowupRules\.IsExactStunPurifySignal\( casterEntityId, actionId, targetEntityId, effect\.Type, effect\.Value, header->GlobalSequence, header->SourceSequence\)\).*?return new MiracleInterceptThreatEvent\( Environment\.TickCount64, localEntityId, casterEntityId, targetEntityId, actionId, effect\.Type, effect\.Value, CurrentMiracleCleanseFollowupGeneration, header->GlobalSequence, header->SourceSequence\)') {
    throw 'Post-Purify Stun capture must scan the fixed eight slots and enqueue only the exact self-Purify 29056 / recovered Stun 1343 signal with non-empty sequence identity.'
}
if ($normalizedMchCapture -notmatch 'var isCleanseFollowup = threat\.ActionId == EnemyCombatConstants\.PurifyActionId; var currentLocalEntityId = isCleanseFollowup \? CurrentMiracleCleanseFollowupLocalEntityId : CurrentMiracleInterceptLocalEntityId;.*?isCleanseFollowup && threat\.FeatureGeneration != CurrentMiracleCleanseFollowupGeneration.*?if \(depth > MaximumQueuedMiracleInterceptThreats\).*?pendingMiracleInterceptThreats\.Enqueue\(threat\)') {
    throw 'The shared bounded Miracle queue must reject stale post-Purify feature generations before enqueueing; it may not add a second queue.'
}

$miracleInterceptRules = Read-RequiredSource (Join-Path $coreRoot 'MiracleInterceptRules.cs') 'Miracle intercept rules'
$normalizedMiracleInterceptRules = $miracleInterceptRules -replace '\s+', ' '
Assert-Literals $miracleInterceptRules @(
    'MarksmanSpiteActionId = 29_415',
    'ZantetsukenActionId = 29_537',
    'FuriousBacklashActionId = 39_188',
    'HardenedScalesStatusId = 4_096',
    'MarksmanSpiteThreatLifetimeMilliseconds = 500',
    'ZantetsukenThreatLifetimeMilliseconds = 500',
    'FuriousBacklashThreatLifetimeMilliseconds = 250',
    'MaximumObservedSignals = 128',
    'targetCount != 1',
    '!additionalEffectsAreCompletelyEmpty',
    'firstEffectType == 0x1B',
    'firstEffectIsCompletelyEmpty',
    'targetEntityId != casterEntityId',
    'targetEntityId == casterEntityId'
) 'Exact Miracle start-marker classifier and bounded one-shot policy'
if ($normalizedMiracleInterceptRules -notmatch 'MarksmanSpiteActionId when targetEntityId != casterEntityId && firstEffectType == 0x1B\s*=>\s*MiracleInterceptThreatKind\.MarksmanSpite' -or
    $normalizedMiracleInterceptRules -notmatch 'ZantetsukenActionId when targetEntityId != casterEntityId && firstEffectIsCompletelyEmpty\s*=>\s*MiracleInterceptThreatKind\.Zantetsuken' -or
    $normalizedMiracleInterceptRules -notmatch 'FuriousBacklashActionId when targetEntityId == casterEntityId && firstEffectIsCompletelyEmpty\s*=>\s*MiracleInterceptThreatKind\.FuriousBacklash') {
    throw 'Pure Miracle classification must retain exact MCH 0x1B, SAM all-empty non-self, and VPR all-empty self signatures.'
}

$miracleCleanseFollowupRules = Read-RequiredSource $miracleCleanseFollowupRulesPath 'Miracle post-Purify Stun follow-up rules'
$normalizedMiracleCleanseFollowupRules = $miracleCleanseFollowupRules -replace '\s+', ' '
Assert-Literals $miracleCleanseFollowupRules @(
    'PurifyActionId = 29_056',
    'StunStatusId = 1_343',
    'ResilienceStatusId = 3_248',
    'RecoveredFromStatusEffectType = 0x10',
    'ResilienceAcquisitionMilliseconds = 750',
    'ResilienceReleaseWaitMilliseconds = 3_000',
    'ResilienceMissingGraceMilliseconds = 150',
    'ReleaseOpportunityMilliseconds = 500',
    'MaximumObservedSignals = 128',
    'casterEntityId == targetEntityId',
    '(globalSequence != 0 || sourceSequence != 0)',
    'ActiveResilienceStatusCount',
    'ResiliencePresenceObserved',
    'ResilienceObservedAtMilliseconds',
    'ResilienceMissingSinceMilliseconds',
    'HigherPriorityClaimed',
    'ReadyForPromotion',
    'PromotionIntent',
    'ReleasedAtMilliseconds >= Signal.ObservedAtMilliseconds',
    'state.ReleasedAtMilliseconds',
    'public bool ShouldPromote',
    'RetiresSignalBeforePromotion'
) 'Exact positive-observation Miracle post-Purify Stun policy'
if ($normalizedMiracleCleanseFollowupRules -notmatch 'IsExactStunPurifySignal\(.*?IsValidEntityId\(casterEntityId\) && casterEntityId == targetEntityId && actionId == PurifyActionId && effectType == RecoveredFromStatusEffectType && effectValue == StunStatusId && \(globalSequence != 0 \|\| sourceSequence != 0\)' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'ValidateCandidate\(signal\.Target, observation\.Candidate\).*?value\.Target != expected \? MiracleCleanseFollowupCancelReason\.CandidateChanged') {
    throw 'The follow-up must bind one exact self-Purify/Stun ActionEffect sequence to one unchanged exact canonical actor.'
}
if ($normalizedMiracleCleanseFollowupRules -notmatch 'if \(age >= ResilienceAcquisitionMilliseconds\).*?ResilienceNotObserved.*?if \(candidate\.ActiveResilienceStatusCount == 0\).*?SignalObserved.*?Waiting.*?Phase = MiracleCleanseFollowupPhase\.WaitingForResilienceEnd, ResiliencePresenceObserved = true, ResilienceObservedAtMilliseconds = nowMilliseconds' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'if \(!state\.ResiliencePresenceObserved \|\| state\.ResilienceObservedAtMilliseconds < 0\).*?if \(candidate\.ActiveResilienceStatusCount == 1\).*?ResilienceMissingSinceMilliseconds = -1.*?if \(state\.ResilienceMissingSinceMilliseconds < 0\).*?ResilienceMissingSinceMilliseconds = observation\.NowMilliseconds.*?if \(missingAge < ResilienceMissingGraceMilliseconds\) return Waiting\(state\);.*?Phase = MiracleCleanseFollowupPhase\.ReleaseOpportunity') {
    throw 'Resilience must be positively observed within 750 ms before 150 ms of continuous live absence can open a release opportunity.'
}
if ($normalizedMiracleCleanseFollowupRules -notmatch 'var age = observation\.NowMilliseconds - state\.ResilienceObservedAtMilliseconds; if \(age < 0\).*?ClockMovedBackwards.*?if \(age >= ResilienceReleaseWaitMilliseconds\).*?ResilienceReleaseTimedOut.*?if \(candidate\.ActiveResilienceStatusCount == 1\)' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'var releaseAge = observation\.NowMilliseconds - state\.ReleasedAtMilliseconds;.*?if \(releaseAge >= ReleaseOpportunityMilliseconds\).*?ReleaseOpportunityExpired.*?if \(observation\.HigherPriorityClaimed\) return Waiting\(state\);.*?new MiracleCleanseFollowupIntent\( signal, state\.ReleasedAtMilliseconds\).*?ReadyForPromotion.*?intent') {
    throw 'The unconditional 3-second hard release deadline must run before every presence/absence/grace path; the 500-ms promotion window then yields to urgent threats without extension.'
}
if ($miracleCleanseFollowupRules -match '\b(UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event|StatusAddress|StatusInstanceToken)\b|\bstatus\.[A-Za-z_]*Address\b|\bstatus\.RemainingTime\b') {
    throw 'Pure post-Purify rules must never dispatch, mutate targets/input, use a status address, or predict release from RemainingTime.'
}

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
    'ActionManager.Instance',
    'configurationEnabled',
    'localPlayerIdentityValid',
    'statusCurrentlyObserved',
    'resilienceActive',
    'allowHeldKeyAtStatusEntry',
    'decision.ShouldConsumeInputGeneration',
    'inputFrame.Consume()',
    'state = decision.NextState'
) 'Emergency Purify probe'
if ($purifyProbe -match '\b(GetAdjustedActionId|GetActionStatus|IsActionOffCooldown|AnimationLock|CurrentMp|PurifyMpCost|CurrentMount|IsTargetable|GetGameObjectId)\b') {
    throw 'Emergency Purify must not restore the fragile local readiness filters removed by the reliability hotfix.'
}
if ([regex]::Matches($purifyProbe, '\bstatusCurrentlyObserved\b').Count -lt 3) {
    throw 'Emergency Purify must require a currently observed exact status for edge authorization and dispatch readiness.'
}
if ($purifyProbe -match '\b(for|foreach|while)\s*\(|\bdo\s*\{' -or
    $purifyProbe -match '\b(Retry|QueuedAction|ActionQueued|Enqueue|Dequeue)\b|\bQueue\s*[<(]' -or
    $purifyProbe -match '\b(IGameInteropProvider|Hook<|HookFromAddress|SignatureAttribute|SigScanner|ITargetManager|TargetManager|SetTarget)\b') {
    throw 'Emergency Purify probe must not loop, retry, queue, hook, scan signatures, or access target mutation APIs.'
}

$emergencyInputCoordinator = Read-RequiredSource $emergencyInputCoordinatorPath 'Shared emergency-action input coordinator'
Assert-Literals $emergencyInputCoordinator @(
    'new GameInputContextProbe(keyState)',
    'probe.Observe()',
    'probe.ConsumeHeldGameplayKeys()',
    'FreshGameplayKeyPressed',
    'HeldGameplayKeyEligible',
    'IsConsumed',
    'if (IsConsumed) return',
    'purifyHeldEnabled',
    'allyRescueHeldEnabled',
    'miracleInterceptHeldEnabled',
    'miracleInterceptHeldWasEnabled',
    'heldOptionJustEnabled',
    'probe.Reset()'
) 'Shared Purify, Ally Rescue, and Miracle input ownership'
if ($emergencyInputCoordinator -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|Hook<|HookFromAddress|ITargetManager|TargetManager)\b') {
    throw 'The shared emergency input coordinator may only observe and consume physical generations.'
}

$personalStatus = Read-RequiredSource $personalStatusPath 'Personal status coordinator'
$normalizedPersonalStatus = $personalStatus -replace '\s+', ' '
$purifyObserve = [regex]::Match($personalStatus, '\bemergencyPurify\.Observe\s*\(')
$rescueObserve = [regex]::Match($personalStatus, '\ballyRescue\.Observe\s*\(')
$miracleObserve = [regex]::Match($personalStatus, '\bmiracleIntercept\.Observe\s*\(')
if (-not $purifyObserve.Success -or -not $rescueObserve.Success -or -not $miracleObserve.Success -or
    $purifyObserve.Index -gt $rescueObserve.Index -or
    $rescueObserve.Index -gt $miracleObserve.Index -or
    [regex]::Matches($personalStatus, '\bemergencyInputFrame\b').Count -lt 4) {
    throw 'Personal status coordination must give self-Purify, Ally Rescue, then Miracle first-to-last claim on one shared input frame.'
}
Assert-Literals $personalStatus @(
    'purifyClaimedPriority',
    'allyRescueConfigurationEnabled && !purifyClaimedPriority',
    'EmergencyActionPriorityRules.AllyRescueClaimsPriority(',
    'miracleInterceptConfigurationEnabled,',
    '!purifyClaimedPriority &&',
    '!allyRescueClaimedPriority',
    'metadata.AllyRescueStatusesVerified',
    'metadata.MiracleOfNatureActionVerified',
    'metadata.MarksmanSpiteVerified',
    'metadata.ZantetsukenVerified',
    'metadata.FuriousBacklashVerified',
    'configuration.MiracleInterceptAfterPurifiedStun',
    'metadata.PurifyVerified',
    'context == SupportedPvPContext.CrystallineConflict'
) 'Shared self-Purify, Ally Rescue, and Miracle priority'
if ($normalizedPersonalStatus -notmatch 'miracleIntercept\.Observe\( localPlayer, context == SupportedPvPContext\.CrystallineConflict, miracleInterceptConfigurationEnabled, !purifyClaimedPriority && !allyRescueClaimedPriority,') {
    throw 'Miracle must receive persistent feature/capture enablement separately from its transient Purify/Rescue dispatch permission.'
}
if ($normalizedPersonalStatus -notmatch 'configuration\.MiracleInterceptMchLimitBreak, configuration\.MiracleInterceptSamZantetsuken, configuration\.MiracleInterceptViperNest, configuration\.MiracleInterceptAfterPurifiedStun, metadata\.MarksmanSpiteVerified, metadata\.ZantetsukenVerified, metadata\.FuriousBacklashVerified, metadata\.PurifyVerified, emergencyInputFrame') {
    throw 'The independently default-off post-Purify subtype and Purify metadata verification must be wired separately after the three urgent Miracle triggers.'
}
$normalizedEmergencyPriority = (Read-RequiredSource (Join-Path $coreRoot 'AllyRescueBufferRules.cs') 'Emergency action priority rules') -replace '\s+', ' '
if ($normalizedEmergencyPriority -notmatch 'AllowMiracleIntercept\( EmergencyPurifyBufferDecision purifyDecision, AllyRescueBufferDecision rescueDecision\)\s*=>\s*!SelfPurifyClaimsPriority\(purifyDecision\)\s*&&\s*!AllyRescueClaimsPriority\(rescueDecision\)') {
    throw 'Core emergency-action priority must permit Miracle only after both self-Purify and Ally Rescue decline the generation.'
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
    'if (decision.ShouldConsumeInputGeneration) inputFrame.Consume()',
    'TryRevalidateCandidate',
    'nearAssist.RunWithoutRedirect',
    'ActionType.Action',
    'ActionManager.UseActionMode.None'
) 'Bounded Ally Rescue runtime'
if ($allyRescue -match '\bstatus\.Address\b|\bIsActionOffCooldown\b') {
    throw 'Ally Rescue must not restore the fragile status-address or local cooldown prefilters removed by the reliability hotfix.'
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
    'MaximumConfirmedKeys = 128'
) 'Exact Ally Rescue confirmation correlation'
if ($normalizedAllyRescueConfirmationRules -notmatch 'IsConfirmableRemovedStatus\(uint statusId\) => statusId is StunStatusId or HeavyStatusId or BindStatusId or SilenceStatusId or MiracleOfNatureStatusId or DeepFreezeStatusId;' -or
    $normalizedAllyRescueConfirmationRules -match 'IsConfirmableRemovedStatus\(uint statusId\) =>[^;]*(?:134[0-9]|30[0-9]{2}|32[0-9]{2})') {
    throw 'Ally Rescue confirmation must accept exactly the six reviewed removable-status constants.'
}
if ($allyRescueConfirmationRules -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|RetryAction|RetryDispatch|ITargetManager|TargetManager|SetTarget)\b') {
    throw 'Pure Ally Rescue confirmation rules must never initiate actions, retry, or access target mutation APIs.'
}

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
    throw 'Ally Rescue must commit its spent state before the sole native action attempt.'
}
if ($allyRescue -match '\b(for|while|do)\s*\([^)]*UseAction' -or
    $allyRescue -match '\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction|ITargetManager|TargetManager|SetTarget)\b') {
    throw 'Ally Rescue must never retry, queue, loop action calls, or mutate visible targets.'
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

# Miracle is the third and final direct action boundary. It must remain CC-only,
# WHM-only, exact-enemy, metadata/protection-gated, and strictly one-shot.
$miracleIntercept = Read-RequiredSource $miracleInterceptProbePath 'Miracle intercept probe'
$normalizedMiracleIntercept = $miracleIntercept -replace '\s+', ' '
if ([regex]::Matches($miracleIntercept, '(?:->|\.)UseAction\s*\(').Count -ne 1) {
    throw 'Miracle intercept must contain exactly one native UseAction call.'
}
Assert-Literals $miracleIntercept @(
    'MiracleInterceptRules.GetThreatLifetimeMilliseconds(kind)',
    'RequiredCcProtectionStatusIds',
    'CcImmunityBrakeActionCatalog',
    'GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.Miracle)',
    '.Append(EnemyCombatConstants.HardenedScalesStatusId)',
    '.Distinct()',
    'EnemyCombatConstants.HardenedScalesStatusId',
    'RequiredCcProtectionStatusIds.All(',
    'verifiedProtectionStatusIds.Contains',
    'isCrystallineConflict',
    'EnemyCombatConstants.WhiteMageJobId',
    'signal.LocalEntityId != localPlayer.EntityId',
    'EnemyCombatConstants.MachinistJobId',
    'EnemyCombatConstants.SamuraiJobId',
    'EnemyCombatConstants.ViperJobId',
    'executeTracker.Enemies',
    'HasAnyVerifiedCcProtection',
    'HasVerifiedActiveStatus',
    'CcImmunityBrakeActionCatalog.IsBlockerStatus(',
    'CcImmunityBrakeBlockerFamily.Miracle',
    'Actor status-list membership is the authoritative live presence',
    'GetActionInRangeOrLoS',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight',
    'activeThreat = null',
    'inputFrame.Consume()',
    'TryUseMiracleOnce(revalidated.GameObjectId, out attempted)',
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
    'bool enablePostPurifyStun',
    'bool purifyMetadataVerified',
    'var cleanseFollowupEnabled = enabled &&',
    'capture.SetMiracleCleanseFollowupLocalEntityId(',
    'MiracleCleanseFollowupRules.ResilienceAcquisitionMilliseconds',
    'signal.FeatureGeneration != capture.CurrentMiracleCleanseFollowupGeneration',
    'ResolveCleanseFollowupCandidate(',
    'CountActiveStatuses(',
    'inputFrame.FreshGameplayKeyPressed',
    'inputFrame.HeldGameplayKeyEligible',
    'MiracleCleanseFollowupRules.Observe(',
    'cleanseFollowupState = decision.NextState',
    'decision.ShouldPromote',
    'decision.PromotionIntent',
    'MiracleInterceptThreatKind.PostPurifyStun',
    'MiracleCleanseFollowupRules.ReleaseOpportunityMilliseconds'
) 'Bounded exact-target WHM Miracle runtime'
if ($normalizedMiracleIntercept -notmatch 'var protectionMetadataReady = RequiredCcProtectionStatusIds\.All\( verifiedProtectionStatusIds\.Contains\); var enabled = configurationEnabled && isCrystallineConflict && localIdentityValid && isWhiteMage && protectionMetadataReady;' -or
    $normalizedMiracleIntercept -notmatch 'DrainThreats\( localPlayer!, enableMarksmanSpite && marksmanSpiteMetadataVerified, enableZantetsuken && zantetsukenMetadataVerified, enableFuriousBacklash && furiousBacklashMetadataVerified && verifiedProtectionStatusIds\.Contains\(EnemyCombatConstants\.HardenedScalesStatusId\), cleanseFollowupEnabled, nowMilliseconds\)' -or
    $miracleIntercept -match '\bShowCcProtection\b') {
    throw 'Miracle must require its complete independent blocker metadata, VPR Hardened Scales metadata, and each independently verified threat before arming.'
}
if ($normalizedMiracleIntercept -notmatch 'var cleanseFollowupEnabled = enabled && enablePostPurifyStun && purifyMetadataVerified;' -or
    $normalizedMiracleIntercept -notmatch 'capture\.SetMiracleCleanseFollowupLocalEntityId\( cleanseFollowupEnabled && localAlive \? localPlayer!\.EntityId : 0\)') {
    throw 'Post-Purify Stun ActionEffect capture must remain separately gated by its own toggle, verified Purify metadata, live WHM identity, and CC-only Miracle master.'
}
if ($normalizedMiracleIntercept -notmatch 'private IPlayerCharacter\? ResolveCleanseFollowupCandidate\( IPlayerCharacter localPlayer, MiracleCleanseFollowupTargetIdentity target\).*?enemy\.GameObjectId == target\.GameObjectId && enemy\.EntityId == target\.EntityId && enemy\.JobId == target\.JobId.*?Take\(2\).*?if \(canonical\.Length != 1\) return null;.*?player\.GameObjectId == target\.GameObjectId && player\.EntityId == target\.EntityId && player\.GameObjectId != localPlayer\.GameObjectId && player\.ClassJob\.IsValid && player\.ClassJob\.RowId == target\.JobId.*?Take\(2\).*?return players\.Length == 1 && IsLivePlayer\(players\[0\]\) && HasValidNativeIdentity\(players\[0\]\)') {
    throw 'Post-Purify status observation must resolve exactly one unchanged canonical e1-e5 and exactly one matching live native player actor.'
}
if ($normalizedMiracleIntercept -notmatch 'var anyProtection = HasAnyVerifiedCcProtection\(candidate\); var hardenedScales = threat\.Kind == MiracleInterceptThreatKind\.FuriousBacklash && HasVerifiedActiveStatus\( candidate, EnemyCombatConstants\.HardenedScalesStatusId\); var otherProtection = anyProtection && !hardenedScales;.*?var locallyReady = !hardenedScales && !otherProtection && rangeAndLineOfSight' -or
    $normalizedMiracleIntercept -notmatch 'var revalidatedHardened = revalidated is not null && threat\.Kind == MiracleInterceptThreatKind\.FuriousBacklash && HasVerifiedActiveStatus\( revalidated, EnemyCombatConstants\.HardenedScalesStatusId\); var revalidatedProtection = revalidated is not null && HasAnyVerifiedCcProtection\(revalidated\);.*?if \(revalidated is not null && !revalidatedHardened && !revalidatedProtection && revalidatedRange\)') {
    throw 'Miracle must prove the narrow live blocker matrix for every threat and live Hardened Scales absence for VPR both before spending input and immediately before UseAction.'
}
if ($normalizedMiracleIntercept -notmatch 'actionManager->UseAction\s*\(\s*ActionType\.Action\s*,\s*EnemyCombatConstants\.MiracleOfNatureActionId\s*,\s*targetGameObjectId\s*,\s*0\s*,\s*ActionManager\.UseActionMode\.None\s*,\s*0\s*\)') {
    throw 'Miracle intercept must issue only ActionType.Action 29228 to the exact revalidated enemy with UseActionMode.None.'
}
$miracleConsumeState = [regex]::Match($miracleIntercept, 'activeThreat\s*=\s*null\s*;\s*\r?\n\s*inputFrame\.Consume\s*\(\s*\)\s*;')
$miracleTryUse = [regex]::Match($miracleIntercept, '\bTryUseMiracleOnce\s*\(\s*revalidated\.GameObjectId')
$miracleNativeCall = [regex]::Match($miracleIntercept, 'actionManager->UseAction\s*\(')
if (-not $miracleConsumeState.Success -or -not $miracleTryUse.Success -or -not $miracleNativeCall.Success -or
    $miracleConsumeState.Index -gt $miracleTryUse.Index -or
    $miracleTryUse.Index -gt $miracleNativeCall.Index) {
    throw 'Miracle intercept must spend its threat and shared input before its one revalidated native action attempt.'
}
if ($miracleIntercept -match '\b(GetAdjustedActionId|GetActionStatus|IsActionOffCooldown|AnimationLock|CurrentMp|CurrentMount|CanUseActionOnTarget)\b' -or
    $miracleIntercept -match '(?-i:\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction)\b)' -or
    $miracleIntercept -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Miracle intercept must never cooldown-prefilter, retry, queue, or mutate a visible target.'
}
if ($miracleIntercept -match '\bstatus\.RemainingTime\b|\bstatus\.[A-Za-z_]*Address\b|\b(StatusAddress|StatusInstanceToken)\b' -or
    $normalizedMiracleIntercept -notmatch 'foreach \(var status in player\.StatusList\).*?verifiedProtectionStatusIds\.Contains\(status\.StatusId\) && CcImmunityBrakeActionCatalog\.IsBlockerStatus\( CcImmunityBrakeBlockerFamily\.Miracle, status\.StatusId, targetJobId\).*?return true' -or
    $normalizedMiracleIntercept -notmatch 'foreach \(var status in player\.StatusList\).*?status\.StatusId == statusId.*?return true' -or
    $normalizedMiracleIntercept -notmatch 'private static int CountActiveStatuses\(IPlayerCharacter player, uint statusId\).*?foreach \(var status in player\.StatusList\).*?if \(status\.StatusId != statusId\) continue; count\+\+; if \(count > 1\) return count;') {
    throw 'Miracle protection and Resilience-release gates must use unambiguous live StatusList membership, never status addresses or RemainingTime prediction.'
}

# The news flash is confirmation of the exact Miracle status application, not a
# claim that the hostile startup or damage was definitely cancelled.
$miracleConfirmationRules = Read-RequiredSource (Join-Path $coreRoot 'MiracleInterceptConfirmationRules.cs') 'Miracle intercept landing confirmation rules'
$normalizedMiracleConfirmationRules = $miracleConfirmationRules -replace '\s+', ' '
Assert-Literals $miracleConfirmationRules @(
    'MiracleOfNatureActionId = 29_228',
    'MiracleOfNatureStatusId = 3_085',
    'AddStatusEffectType = 0x0E',
    'CorrelationMilliseconds = 1_500',
    'PopupDurationMilliseconds = 1_500',
    'MaximumConfirmedKeys = 128',
    'MiracleInterceptThreatKind.MarksmanSpite',
    'MiracleInterceptThreatKind.Zantetsuken',
    'MiracleInterceptThreatKind.FuriousBacklash',
    'MiracleInterceptThreatKind.PostPurifyStun',
    'observation.CasterEntityId == pending.LocalCasterEntityId',
    'observation.ActionId == pending.ActionId',
    'observation.TargetEntityId == pending.TargetEntityId',
    'observation.EffectType == AddStatusEffectType',
    'observation.EffectValue == MiracleOfNatureStatusId',
    'observation.GlobalSequence != 0 || observation.SourceSequence != 0',
    'previous.ConfirmedKeys.Contains(key)',
    'AppendBounded(previous.ConfirmedKeys, key)',
    'TotalConfirmed = SaturatingIncrement(previous.TotalConfirmed)',
    'PendingInsideWindow(previous.Pending, nowMilliseconds) is { } activePending',
    'Pending = activePending',
    'This proves that Miracle landed; it does not prove that the hostile action',
    'damage was cancelled.'
) 'Exact bounded Miracle landing correlation and popup truth claim'
if ($normalizedMiracleConfirmationRules -notmatch 'observation\.ObservedAtMilliseconds < pending\.AttemptedAtMilliseconds \|\| observation\.ObservedAtMilliseconds - pending\.AttemptedAtMilliseconds > CorrelationMilliseconds' -or
    $normalizedMiracleConfirmationRules -notmatch 'var skip = Math\.Max\(0, previous\.Length - MaximumConfirmedKeys \+ 1\); return previous\.Skip\(skip\)\.Append\(key\)\.ToImmutableArray\(\)' -or
    $normalizedMiracleConfirmationRules -notmatch 'if \(PendingInsideWindow\(previous\.Pending, nowMilliseconds\) is \{ \} activePending\) \{ return None\(previous with \{ Pending = activePending, Popup = ActivePopup\(previous\.Popup, nowMilliseconds\), LastObservedAtMilliseconds = nowMilliseconds, \}\); \} if \(!attempt\.IsValid \|\| attempt\.AttemptedAtMilliseconds != nowMilliseconds\).*?Pending = attempt') {
    throw 'Miracle landing correlation must be forward-only within 1500 ms, preserve the first active pending attempt before accepting another, and deduplicate through a bounded 128-key history.'
}
if ($miracleConfirmationRules -match '\b(UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event)\b') {
    throw 'Miracle landing confirmation rules must remain observational and never initiate actions, input, or target changes.'
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
    'attempted && revalidated is not null && attemptedAtMilliseconds >= 0',
    'ConfirmationPopup = confirmationState.Popup',
    'ConfirmedLandingCount = confirmationState.TotalConfirmed',
    'ConfirmationQueueDepth = capture.MiracleInterceptConfirmationQueueDepth',
    'CapturedConfirmationCount = capture.CapturedMiracleInterceptConfirmations',
    'DroppedConfirmationCount = capture.DroppedMiracleInterceptConfirmations',
    'bool dispatchAllowed',
    'confirmationPendingForLocalCaster',
    'enabled && (localAlive || confirmationPendingForLocalCaster)',
    'Waiting for exact Miracle landing evidence'
) 'Miracle landing runtime correlation and diagnostics'
$miracleRegisterIndex = $normalizedMiracleIntercept.IndexOf('MiracleInterceptConfirmationRules.RegisterAttempt(')
$miracleTryUseIndex = $normalizedMiracleIntercept.IndexOf('TryUseMiracleOnce(revalidated.GameObjectId')
if ($miracleTryUseIndex -lt 0 -or $miracleRegisterIndex -le $miracleTryUseIndex -or
    $normalizedMiracleIntercept -notmatch 'if \(attempted && revalidated is not null && attemptedAtMilliseconds >= 0\) \{ var registered = MiracleInterceptConfirmationRules\.RegisterAttempt') {
    throw 'Miracle confirmation may register only after this helper actually made its sole native attempt against the revalidated exact target.'
}
$miracleDrainConfirmationIndex = $normalizedMiracleIntercept.IndexOf('DrainConfirmations(nowMilliseconds)')
$miracleFollowupIndex = $normalizedMiracleIntercept.IndexOf('ObserveCleanseFollowup(')
$miracleNoThreatIndex = $normalizedMiracleIntercept.IndexOf('if (activeThreat is not { } threat)')
$miracleDispatchGateIndex = $normalizedMiracleIntercept.IndexOf('if (!dispatchAllowed)')
if ($miracleDrainConfirmationIndex -lt 0 -or
    $miracleFollowupIndex -le $miracleDrainConfirmationIndex -or
    $miracleNoThreatIndex -le $miracleFollowupIndex -or
    $miracleDispatchGateIndex -le $miracleDrainConfirmationIndex -or
    $normalizedMiracleIntercept -notmatch 'if \(activeThreat is \{ \} expiringThreat && \(nowMilliseconds < expiringThreat\.ObservedAtMilliseconds \|\| nowMilliseconds - expiringThreat\.ObservedAtMilliseconds >= ThreatLifetime\(expiringThreat\.Kind\)\)\) \{ RecordExpired\(expiringThreat\); activeThreat = null; \}.*?ObserveCleanseFollowup\( localPlayer!, cleanseFollowupEnabled, !dispatchAllowed \|\| activeThreat is not null, null, nowMilliseconds\); if \(activeThreat is not \{ \} threat\) return Publish\("Waiting", "No current exact threat", nowMilliseconds\);.*?if \(!dispatchAllowed\) \{ RecordWait\(threat, MiracleWaitReason\.HigherPriorityHelper\); return Publish\("Armed", "Waiting: higher-priority helper claimed this frame", nowMilliseconds\); \}' -or
    $normalizedMiracleIntercept -notmatch 'if \(!localAlive\).*?if \(confirmationPendingForLocalCaster\) DrainConfirmations\(nowMilliseconds\);.*?"Waiting for exact Miracle landing evidence"') {
    throw 'Every follow-up frame must run before the sole dispatch decision; urgent/helper priority may only retain it inside its original TTL, while local death must preserve exact pending landing evidence.'
}
if ([regex]::Matches($normalizedMiracleIntercept, 'ObserveCleanseFollowup\( localPlayer!, cleanseFollowupEnabled, !dispatchAllowed \|\| activeThreat is not null,').Count -ne 2) {
    throw 'Both new-signal and ordinary-frame follow-up observations must yield to urgent MCH/SAM/VPR threats and higher-priority Purify/Rescue input ownership.'
}
if ($normalizedMiracleIntercept -notmatch 'cleanseFollowupState = decision\.NextState;.*?if \(!decision\.ShouldPromote \|\| decision\.PromotionIntent is not \{ \} promotion\) return;.*?if \(activeThreat is not null\).*?return;.*?activeThreat = new MiracleThreatState\( MiracleInterceptThreatKind\.PostPurifyStun, promotion\.Target\.GameObjectId, promotion\.Target\.EntityId, promotion\.Target\.JobId, promotion\.ReleasedAtMilliseconds' -or
    $normalizedMiracleIntercept -notmatch 'private static long ThreatLifetime\(MiracleInterceptThreatKind kind\) => kind == MiracleInterceptThreatKind\.PostPurifyStun \? MiracleCleanseFollowupRules\.ReleaseOpportunityMilliseconds : MiracleInterceptRules\.GetThreatLifetimeMilliseconds\(kind\);') {
    throw 'The exact post-Purify state must be retired before promotion, and the shared dispatcher must measure its unextended 500 ms from the original verified release edge.'
}
if ($normalizedMiracleIntercept -match 'MiracleInterceptThreatKind\.PostPurifyStun,.*?decision\.NextState\.LastObservedAtMilliseconds') {
    throw 'Priority-delayed post-Purify promotion must never restart its 500-ms TTL from the later framework decision time.'
}
$miraclePriorityBranch = [regex]::Match(
    $normalizedMiracleIntercept,
    'if \(!dispatchAllowed\) \{(?<Body>.*?)\} var candidate = ResolveCandidate')
if (-not $miraclePriorityBranch.Success -or
    $miraclePriorityBranch.Groups['Body'].Value -match 'activeThreat\s*=|inputFrame\.Consume|TryUseMiracleOnce|UseAction|ObservedAtMilliseconds\s*=') {
    throw 'A transient higher-priority helper may wait only: it must not clear or extend the threat, consume/reuse input, or initiate/replay Miracle.'
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
Assert-Literals $overlaySource @(
    'miracle.ConfirmationPopup is { } miraclePopup && miraclePopup.IsVisible(now)',
    'DrawMiracleInterceptConfirmationCard(',
    '"MIRACLE LANDED"',
    '"INTERRUPT ATTEMPT  •  MCH LB"',
    '"INTERRUPT ATTEMPT  •  SAM LB"',
    '"INTERRUPT ATTEMPT  •  VPR NEST"',
    '"CC FOLLOW-UP  •  RESILIENCE ENDED"',
    'MiracleInterceptConfirmationRules.PopupDurationMilliseconds'
) 'Visible, bounded, non-overclaiming Miracle news flash'
if ($overlaySource -match '(?i)interrupt(?:ed| successful| confirmed)|cancelled hostile|stopped (?:mch|sam|vpr|lb|nest)') {
    throw 'Miracle news flash may say Miracle landed/interrupt attempt, but may not claim the hostile action was proven interrupted.'
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
if (-not $monkObserve.Success -or $monkObserve.Index -lt $miracleObserve.Index -or
    $normalizedPersonalStatus -notmatch 'var isSupportedPvPContext = context != SupportedPvPContext\.None' -or
    $normalizedPersonalStatus -notmatch 'monkEarthReply\.Observe\( localPlayer, isSupportedPvPContext, configuration\.Enabled && configuration\.EnableMonkEarthReplyHelper, metadata\.MonkEarthReplyVerified, configuration\.MonkEarthReplyOnLowHp, configuration\.MonkEarthReplyBeforeExpiry, configuration\.MonkEarthReplyHpPercent, configuration\.MonkEarthReplyExpirySeconds, purifyClaimedPriority \|\| rescue\.UseActionAttempted \|\| miracle\.UseActionAttempted') {
    throw 'Monk Earth Reply must run after Purify/Rescue/Miracle, use CC or opted Wolves Den plus verified metadata, and yield whenever an earlier helper attempted an action.'
}

$targetPressureTracker = Read-RequiredSource (Join-Path $pluginServicesRoot 'TargetPressureTracker.cs') 'Target pressure tracker'
$normalizedTargetPressureTracker = $targetPressureTracker -replace '\s+', ' '
if ($normalizedTargetPressureTracker -notmatch 'configuration\.ExperimentalAllyRescueOnNextKey\s*&&\s*metadata\.AllyRescueStatusesVerified\s*&&\s*supportedContext\s*==\s*SupportedPvPContext\.CrystallineConflict') {
    throw 'Incoming Ally Rescue pressure tracking must require verified statuses and remain CC-only.'
}
$allyRescueBuffer = Read-RequiredSource (Join-Path $coreRoot 'AllyRescueBufferRules.cs') 'Ally Rescue one-generation rules'
Assert-Literals $allyRescueBuffer @(
    'DefaultBufferMilliseconds = 750',
    'MaximumBufferMilliseconds = 750',
    'SpentIntents',
    'ResolveCandidateEntryTrigger',
    'AllowHeldKeyAtCandidateEntry',
    'current.SpentIntents.Add(intent)',
    'Kind is AllyRescueBufferDecisionKind.Armed or AllyRescueBufferDecisionKind.Dispatch'
) 'Ally Rescue one-generation no-retry rules'

$nearAssist = Read-RequiredSource $nearAssistPath 'Near Assist redirector'
$normalizedNearAssist = $nearAssist -replace '\s+', ' '
Assert-Literals $nearAssist @(
    'HookFromAddress<ActionManager.Delegates.UseAction>',
    'ActionManager.MemberFunctionPointers.UseAction',
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
if ([regex]::Matches($nearAssist, '\buseActionHook!\.Original\s*\(').Count -ne 1) {
    throw 'Near Assist must call the hook Original exactly once from its detour.'
}
if ($nearAssist -match '(?:->|\.)UseAction\s*\(' -or
    $nearAssist -match '(?-i:\b(UseActionLocation|ExecuteAction|SendAction|ActionQueued|QueuedAction|QueueAction|RetryAction|RetryDispatch)\b)' -or
    $nearAssist -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Near Assist may forward one Original call but must never initiate, retry, queue, or visibly mutate a target.'
}
if ($normalizedNearAssist -notmatch 'useActionHook!\.Original\s*\(\s*thisPtr\s*,\s*actionType\s*,\s*actionId\s*,\s*forwardedTargetId\s*,\s*extraParam\s*,\s*mode\s*,\s*comboRouteId\s*,\s*outOptAreaTargeted\s*\)') {
    throw 'Near Assist Original must preserve every native action argument except the bounded forwardedTargetId.'
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
if ($normalizedUseActionDetour -notmatch 'if \(!bypassRedirect\) \{ try \{ var resolvedActionId = ResolveActionId\(thisPtr, actionType, actionId\); if \(ccImmunityBrake\.ShouldBlock\( actionType, resolvedActionId, targetId, forwardedTargetId, targetSuppressedByRedirect, mode\)\) \{ return false; \} \} catch \(Exception exception\) \{ ccImmunityBrake\.RecordFailedOpen\(exception\); \} \}') {
    throw 'The brake must inspect original and final target identities, return false before Original only on an exact Block decision, and let pass/fail-open paths reach the sole Original.'
}
$brakeDetourSection = [regex]::Match(
    $normalizedUseActionDetour,
    'if \(!bypassRedirect\) \{ try \{ var resolvedActionId = ResolveActionId.*?ccImmunityBrake\.RecordFailedOpen\(exception\); \} \}').Value
if ([regex]::Matches($brakeDetourSection, '\breturn false;').Count -ne 1 -or
    $brakeDetourSection -match 'forwardedTargetId\s*=\s*InvalidCarrierTargetId|useActionHook!\.Original\s*\(' -or
    $brakeDetourSection -notmatch 'catch \(Exception exception\) \{ ccImmunityBrake\.RecordFailedOpen\(exception\); \}') {
    throw 'A confirmed brake block must make zero Original calls via one direct false return; it must never use target-zero suppression, while exceptions must fail open without changing the final target.'
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
    $allDirectZeroAssignments.Count -ne 5 -or
    $allConditionalZeroAssignments.Count -ne 2) {
    throw 'Every reviewed Near/Far path that can author target zero must set explicit targetSuppressedByRedirect provenance before the CC brake inspects the call.'
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
if ($normalizedCcImmunityBrake -notmatch 'for \(var slot = EnemySlotRules\.FirstSlot; slot <= EnemySlotRules\.LastSlot; slot\+\+\).*?EnemySlotResolver\.Resolve\(objectTable, slot\)' -or
    $normalizedCcImmunityBrake -notmatch 'targetId == candidate\.GameObjectId \|\| targetId == candidate\.EntityId' -or
    $normalizedCcImmunityBrake -notmatch 'var liveStatuses = target\?\.StatusList \.Select\(static status => status\.StatusId\) \.Where\(verifiedStatusIds\.Contains\) \.ToArray\(\)') {
    throw 'CC-brake target resolution must scan exact e1-e5 identities and sample the resolved actor''s live StatusList at action time.'
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
    'RunWithoutRedirect<T>',
    '[ThreadStatic]',
    'internalRedirectBypassDepth++',
    'internalRedirectBypassDepth--',
    'finally',
    'var bypassRedirect = internalRedirectBypassDepth > 0',
    'if (!bypassRedirect &&'
) 'Near Help shared redirector'
if ([regex]::Matches($nearAssist, 'if\s*\(!bypassRedirect\s*&&').Count -ne 4) {
    throw 'Plugin-owned direct helper calls must bypass legacy Far Help suppression plus the Near Assist, Near Help, and Far Help branches without consuming any macro token.'
}
$nearHelpSelection = Read-RequiredSource (Join-Path $coreRoot 'NearHelpSelectionRules.cs') 'Near Help selection rules'
Assert-Literals $nearHelpSelection @(
    'candidate.CurrentHp * current.MaximumHp',
    'current.CurrentHp * candidate.MaximumHp',
    'candidate.DistanceSquared.CompareTo(current.DistanceSquared)',
    'candidate.IsExactFriendly',
    '!candidate.IsSelf',
    'candidate.HasValidActionTarget',
    'candidate.HasRangeAndLineOfSight'
) 'Near Help selection rules'
$nearHelpOneShot = Read-RequiredSource (Join-Path $coreRoot 'NearHelpOneShotRules.cs') 'Near Help one-shot rules'
Assert-Literals $nearHelpOneShot @(
    'DefaultLifetimeMilliseconds = 750',
    'NearHelpOneShotState.Initial',
    'NearHelpSelectionRules.SelectBestIndex',
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
    'case 29066:.*?expectedJobId = 19;.*?maximumDistance = 10f;',
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
if ($normalizedNearAssist -notmatch 'distanceSquared < maximumDistance \* maximumDistance') {
    throw 'Guardian must retain the strict under-10-yalm candidate limit; equality is not accepted.'
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
if ($normalizedNearAssist -notmatch 'var hadToken = armedTarget is not null \|\| oneShotState\.IsArmed \|\| armedHelpTarget is not null \|\| nearHelpState\.IsArmed \|\| armedFarHelpTarget is not null \|\| farHelpState\.IsArmed \|\| farHelpFallbackSuppressionState\.IsArmed;.*?armedTarget = null;.*?armedHelpTarget = null;.*?armedFarHelpTarget = null;.*?farHelpFallbackSuppressionState = FarHelpFallbackSuppressionState\.Initial;') {
    throw 'Near Assist, Near Help, and Far Help tokens must be mutually exclusive and cleared together.'
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
    'counts[pair.Value] = counts.GetValueOrDefault(pair.Value) + 1'
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
    'IncomingEvidence != TargetPressureEvidence.None'
) 'Immutable target pressure runtime snapshot'
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
    'DrawCcProtectionEmblem(anchor, activeProtections, now)',
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

$settingsWindow = Read-RequiredSource (Join-Path $pluginUiRoot 'SettingsWindow.cs') 'Settings window'
$normalizedSettingsWindow = $settingsWindow -replace '\s+', ' '
Assert-Literals $settingsWindow @(
    'ImGui.BeginTabItem("Jobs")',
    'DrawJobsTab()',
    'ALL JOBS / GENERAL QUALITY OF LIFE',
    'DrawResourceAuraControls()',
    '"NINJA"',
    '"MONK"',
    'DrawMonkEarthReplyControls()',
    '"BARD / WHITE MAGE"',
    '"WHITE MAGE"'
) 'Jobs quality-of-life settings organization'
if ($normalizedSettingsWindow -notmatch 'private bool DrawJobsTab\(\).*?ALL JOBS / GENERAL QUALITY OF LIFE.*?DrawResourceAuraControls\(\).*?"NINJA".*?"MONK".*?DrawMonkEarthReplyControls\(\).*?"BARD / WHITE MAGE".*?"WHITE MAGE"') {
    throw 'Jobs tab must keep general, Ninja, Monk, BRD/WHM, then WHM sections in reviewable order.'
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
    'ValidateFeature("Zantetsuken"',
    'ValidateFeature("Furious Backlash"',
    'MiracleOfNatureActionVerified',
    'ZantetsukenVerified',
    'FuriousBacklashVerified',
    'Forcibly transforms target',
    'preventing them from using actions other than Purify',
    'nullifies status afflictions that can be removed by Purify'
) 'Metadata guard'

$exactCombatIds = [ordered]@{
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
    'new AllyRescueProbe(',
    'emergencyPurify.Observe',
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
$tryUsePurify = [regex]::Match($purifyProbe, '\bTryUsePurifyOnce\s*\(')
if (-not $stateAssignment.Success -or -not $tryUsePurify.Success -or $stateAssignment.Index -gt $tryUsePurify.Index) {
    throw 'Emergency Purify runtime must assign the decision NextState before calling TryUsePurifyOnce.'
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
    throw 'Emergency Purify must store state and consume the physical key generation before attempting Purify.'
}
if ([regex]::Matches($purifyProbe, '\bTryUsePurifyOnce\s*\(').Count -ne 2) {
    throw 'Emergency Purify probe must have one TryUsePurifyOnce call site and one method definition.'
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

$configurationPath = Join-Path $sourceRoot 'SeitonSense.Plugin\Models\PluginConfiguration.cs'
$configuration = Read-RequiredSource $configurationPath 'Plugin configuration'
$normalizedConfiguration = $configuration -replace '\s+', ' '
Assert-Literals $configuration @(
    'public int Version { get; set; } = 16',
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
    'Version = 16',
    'NormalizeCcBrakeSelections()',
    'IsCcBrakeJobEnabled(uint jobId)',
    'IsCcBrakeActionEnabled(uint actionId)',
    'if (actionId is 29244 or 29248)',
    'normalizedActions[29248] = gravityEnabled',
    'Math.Clamp(NearAssistMaxAllyDistance, 5f, 30f)',
    'Math.Clamp(MchLimitBreakSoundId, 1, 16)',
    'Clamp(CcProtectionEmblemScale, 0.75f, 1.75f, 1f',
    'Clamp(ResourceAuraIntensity, 0.1f, 1.5f, 0.8f',
    'Clamp(ResourceAuraPulseSpeed, 0.2f, 2f, 0.75f',
    'Math.Clamp(ResourceAuraHpPercent, 10, 80)',
    'Math.Clamp(ResourceAuraMpThreshold, 0, 10_000)',
    'Math.Clamp(MonkEarthReplyHpPercent, 10, 80)',
    'MonkEarthReplyExpirySeconds,',
    '0.5f,',
    '2.5f,'
) 'Schema-16 held-key, target, resource-aura, job-helper, pressure, immunity-brake, and warning configuration migration'
if ($configuration -notmatch '(?m)^\s*public bool ExperimentalMiracleInterceptOnHeldKey \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool MiracleInterceptMchLimitBreak \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool MiracleInterceptSamZantetsuken \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool MiracleInterceptViperNest \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool MiracleInterceptAfterPurifiedStun \{ get; set; \}\s*$') {
    throw 'New installations must default Miracle master and post-Purify Stun subtype off while the three urgent threat toggles remain on.'
}
if ([regex]::Matches($configuration, '\bExperimentalMiracleInterceptOnHeldKey\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bMiracleInterceptMchLimitBreak\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bMiracleInterceptSamZantetsuken\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bMiracleInterceptViperNest\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bMiracleInterceptAfterPurifiedStun\s*=\s*false\s*;').Count -lt 2) {
    throw 'Schema 16 must keep Miracle master and post-Purify Stun subtype default-off in migration/reset, with all three urgent triggers default-on.'
}
if ([regex]::Matches($configuration, '\bVersion\s*=\s*16\s*;').Count -lt 2 -or
    $normalizedConfiguration -notmatch 'if \(Version >= 16\).*?return;.*?if \(Version < 16\).*?MiracleInterceptAfterPurifiedStun = false;.*?Version = 16;') {
    throw 'Schema 16 must fast-path current settings and migrate/reset the new hostile-action subtype to off before persisting version 16.'
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

Write-Host "Seiton Sense v0.12.0.0 safety contract verified across $($sourceFiles.Count) source files; Near Assist, Near Help, Far Help, and the default-off CC-immunity brake share one bounded target-only detour: an exact live e1-e5 immunity block returns false before downstream Original, while pass/fail-open calls reach Original exactly once; only an unchanged native zero/0xE0000000 carrier may be inspected through the same stable hard target without changing the forwarded target, while explicit redirect provenance keeps every plugin-suppressed zero unresolved and target-zero suppression stays limited to reviewed Near/Far carrier policies; the independently default-off post-Purify Stun subtype reuses the single bounded ActionEffect hook/queue, accepts only an exact generation-bound enemy self-Purify 29056 / 0x10 / Stun 1343 signal, requires positive live Resilience 3248 within 750 ms, enforces the unconditional 3000-ms hard release deadline before its 150-ms absence grace, never uses a status address or RemainingTime prediction, yields to urgent threats, and promotes for only 500 ms into the existing consume-before-sole-UseAction path; Miracle landing correlation preserves its first active pending attempt; the native-hotbar aura uses the visible ActionBarSlotVector OwnerNode union with no container fallback; one shared input generation keeps self-Purify, Ally Rescue, then WHM Miracle priority without input reuse or replay, and persisted opportunity counters explain protection, range, input, priority, rejection, and expiry outcomes; the bounded Miracle news flash reports only an exact confirmed 29228/0x0E/3085 landing, never a proven hostile interrupt."
