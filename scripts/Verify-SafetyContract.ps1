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
$defensiveUtilityProbePath = Join-Path $pluginServicesRoot 'DefensiveUtilityProbe.cs'
$ninjaSeitonProbePath = Join-Path $pluginServicesRoot 'NinjaSeitonDispatchProbe.cs'
$isolationAwarenessPath = Join-Path $pluginServicesRoot 'IsolationAwarenessService.cs'
$autoEnemyFocusMarkPath = Join-Path $pluginServicesRoot 'AutoEnemyFocusMarkService.cs'
$monkEarthReplyProbePath = Join-Path $pluginServicesRoot 'MonkEarthReplyProbe.cs'
$resourceAuraAnchorPath = Join-Path $pluginServicesRoot 'ResourceAuraAnchorTracker.cs'
$allyRescueConfirmationRulesPath = Join-Path $coreRoot 'AllyRescueConfirmationRules.cs'
$miracleCleanseFollowupRulesPath = Join-Path $coreRoot 'MiracleCleanseFollowupRules.cs'
$defensiveUtilityRulesPath = Join-Path $coreRoot 'DefensiveUtilityRules.cs'
$ninjaSeitonDispatchRulesPath = Join-Path $coreRoot 'NinjaSeitonDispatchRules.cs'
$isolationWarningRulesPath = Join-Path $coreRoot 'IsolationWarningRules.cs'
$autoEnemyFocusMarkRulesPath = Join-Path $coreRoot 'AutoEnemyFocusMarkRules.cs'
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
    $defensiveUtilityProbePath,
    $ninjaSeitonProbePath,
    $isolationAwarenessPath,
    $autoEnemyFocusMarkPath,
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
# one exact job-gated ally-rescue, the exact defensive Guard/Guardian boundary,
# one exact WHM/BRD reactive-CC boundary, one exact default-off NIN Seiton
# boundary, and one exact default-off Monk Earth's Reply call. Near
# Assist/Near Help/Far Help may only forward an incoming action through their sole Original.
$actionMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(UseAction|UseActionLocation|ExecuteAction|SendAction)\b')
$unexpectedAction = @($actionMatches | Where-Object {
    $reviewedActionBoundary =
        $_.Path -in @($purifyProbePath, $defensiveUtilityProbePath, $allyRescueProbePath, $miracleInterceptProbePath, $ninjaSeitonProbePath, $monkEarthReplyProbePath, $nearAssistPath) -and
        $_.Line -match '\bUseAction\b'
    $reviewedBrakeDocumentation =
        $_.Path -eq $ccImmunityBrakeTargetRulesPath -and
        $_.Line -match '^\s*///.*\bUseAction\b'
    -not ($reviewedActionBoundary -or $reviewedBrakeDocumentation)
})
if ($unexpectedAction.Count -gt 0) {
    $locations = $unexpectedAction | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only EmergencyPurifyProbe, DefensiveUtilityProbe, AllyRescueProbe, MiracleInterceptProbe, NinjaSeitonProbe, MonkEarthReplyProbe, and the bounded shared macro detour may reference UseAction: $($locations -join ', ')"
}

# Team Attack-1 is the sole reviewed shell-command boundary. It may issue only
# ten compile-time commands: attack1/off paired with exact CC enemy slots e1-e5.
$shellApiMatches = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '\b(RaptureShellModule|GetRaptureShellModule|ExecuteCommandInner|Utf8String\.FromString|MarkingController)\b')
$unexpectedShellApis = @($shellApiMatches | Where-Object { $_.Path -ne $autoEnemyFocusMarkPath })
if ($unexpectedShellApis.Count -gt 0) {
    $locations = $unexpectedShellApis | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Only AutoEnemyFocusMarkService may read Attack-1 telemetry or invoke the reviewed shell boundary: $($locations -join ', ')"
}
$autoEnemyFocusMark = Read-RequiredSource $autoEnemyFocusMarkPath 'Auto enemy focus mark service'
Assert-Literals $autoEnemyFocusMark @(
    'UIModule.Instance()',
    'uiModule->GetRaptureShellModule()',
    'Utf8String.FromString(exactHardcodedCommand)',
    'shell->ExecuteCommandInner(command, uiModule)',
    'command->Dtor(true)'
) 'Single reviewed RaptureShell command execution boundary'
if ([regex]::Matches($autoEnemyFocusMark, '\bExecuteCommandInner\s*\(').Count -ne 1 -or
    [regex]::Matches($autoEnemyFocusMark, '\bUtf8String\.FromString\s*\(').Count -ne 1 -or
    [regex]::Matches($autoEnemyFocusMark, '\bMarkingController\.Instance\s*\(').Count -lt 2 -or
    [regex]::Matches($autoEnemyFocusMark, '\bTryExecuteShellCommand\s*\(').Count -ne 11 -or
    $autoEnemyFocusMark -notmatch 'private static unsafe bool TryExecuteShellCommand\(string exactHardcodedCommand\)') {
    throw 'Team Attack-1 must retain one shell execution call, one UTF-8 command boundary, and reviewed live/dispose marker telemetry reads.'
}
$markerCommandLiterals = @([regex]::Matches($autoEnemyFocusMark, '"(?<Command>/mk (?:attack1|off) <e[1-5]>)"') |
    ForEach-Object { $_.Groups['Command'].Value })
$expectedMarkerCommands = @(
    '/mk attack1 <e1>', '/mk attack1 <e2>', '/mk attack1 <e3>', '/mk attack1 <e4>', '/mk attack1 <e5>',
    '/mk off <e1>', '/mk off <e2>', '/mk off <e3>', '/mk off <e4>', '/mk off <e5>'
)
$actualMarkerCommandSet = ($markerCommandLiterals | Sort-Object) -join '|'
$expectedMarkerCommandSet = ($expectedMarkerCommands | Sort-Object) -join '|'
$allMarkerCommandLiterals = @(Select-String -LiteralPath $sourceFiles.FullName -Pattern '"/mk\s+')
if ($markerCommandLiterals.Count -ne 10 -or
    $actualMarkerCommandSet -ne $expectedMarkerCommandSet -or
    $allMarkerCommandLiterals.Count -ne 10) {
    throw 'Team Attack-1 shell allowlist must contain exactly hardcoded attack1/off commands for e1-e5 and no other /mk literal.'
}
if ($autoEnemyFocusMark -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=|Markers\s*\[[^\]]+\]\s*=|MarkerTimes\s*\[[^\]]+\]\s*=') {
    throw 'Team Attack-1 may not mutate a target or write raw marker memory.'
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
    throw 'Reactive-CC landing capture must require the exact local caster, a non-self network target, an action-specific WHM/BRD status, and AddStatus 0x0E before bounded enqueue.'
}
if ($normalizedMchCapture -notmatch 'MiracleInterceptRules\.ClassifyExactStartSignal\( actionId, casterEntityId, targetEntityId, header->NumTargets, targetEffects\[0\]\.Type, IsEmpty\(targetEffects\[0\]\), HasOnlyEmptyAdditionalEffects\(targetEffects\), header->AnimationVariation\)' -or
    $normalizedMchCapture -notmatch 'for \(var index = 1; index < effects\.Length; index\+\+\).*?!IsEmpty\(effects\[index\]\)') {
    throw 'Reactive-CC threat capture must pass exact single-target identity, all eight effect-slot facts, and animation variation to the pure classifier.'
}

if ($normalizedMchCapture -notmatch 'var localEntityId = actionId == EnemyCombatConstants\.PurifyActionId \? CurrentMiracleCleanseFollowupLocalEntityId : CurrentMiracleInterceptLocalEntityId; var featureGeneration = actionId == EnemyCombatConstants\.PurifyActionId \? CurrentMiracleCleanseFollowupGeneration : CurrentMiracleInterceptGeneration; if \(!IsNetworkEntityId\(localEntityId\) \|\| casterEntityId == localEntityId\) return null;' -or
    $normalizedMchCapture -notmatch 'public void SetMiracleCleanseFollowupLocalEntityId\(uint entityId\).*?ref miracleCleanseFollowupLocalEntityIdBits.*?if \(previous != normalized\) Interlocked\.Increment\(ref miracleCleanseFollowupGeneration\)') {
    throw 'Post-Purify CC capture must use its own opt-in local-identity gate and generation, independently from ordinary reactive-CC capture.'
}
if ($normalizedMchCapture -notmatch 'if \(actionId == EnemyCombatConstants\.PurifyActionId\) \{ ushort removedStatusId = 0; for \(var slot = 0; slot < EffectSlotsPerTarget; slot\+\+\) \{ var effect = targetEffects\[slot\]; if \(!MiracleCleanseFollowupRules\.IsExactPurifySignal\( casterEntityId, actionId, targetEntityId, effect\.Type, effect\.Value, header->GlobalSequence, header->SourceSequence\)\).*?if \(removedStatusId == 0 \|\| PurifyRemovalPriority\(effect\.Value\) > PurifyRemovalPriority\(removedStatusId\)\).*?return removedStatusId == 0 \|\| featureGeneration != CurrentMiracleCleanseFollowupGeneration \|\| localEntityId != CurrentMiracleCleanseFollowupLocalEntityId \? null : new MiracleInterceptThreatEvent\( Environment\.TickCount64, localEntityId, casterEntityId, targetEntityId, actionId, header->AnimationVariation, MiracleCleanseFollowupRules\.RecoveredFromStatusEffectType, removedStatusId, featureGeneration') {
    throw 'Post-Purify CC capture must collapse the fixed eight slots to one deterministic exact self-Purify recovery from the six reviewed removable statuses.'
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
    'MiracleInterceptThreatKind.PostPurifyCrowdControl => 1'
) 'Exact reactive-CC start-marker classifier and bounded one-shot policy'
if ($normalizedMiracleInterceptRules -notmatch 'MarksmanSpiteActionId when targetEntityId != casterEntityId && firstEffectType == 0x1B\s*=>\s*MiracleInterceptThreatKind\.MarksmanSpite' -or
    $normalizedMiracleInterceptRules -notmatch 'ZantetsukenActionId when targetEntityId != casterEntityId && firstEffectIsCompletelyEmpty\s*=>\s*MiracleInterceptThreatKind\.Zantetsuken' -or
    $normalizedMiracleInterceptRules -notmatch 'FuriousBacklashActionId when targetEntityId == casterEntityId && firstEffectIsCompletelyEmpty\s*=>\s*MiracleInterceptThreatKind\.FuriousBacklash' -or
    $normalizedMiracleInterceptRules -notmatch 'ContradanceActionId when targetEntityId == casterEntityId && animationVariation == 0 && firstEffectIsCompletelyEmpty\s*=>\s*MiracleInterceptThreatKind\.Contradance' -or
    $normalizedMiracleInterceptRules -notmatch 'MarksmanSpite or MiracleInterceptThreatKind\.Zantetsuken or MiracleInterceptThreatKind\.FuriousBacklash => 3, MiracleInterceptThreatKind\.Contradance => 2, MiracleInterceptThreatKind\.PostPurifyCrowdControl => 1') {
    throw 'Pure reactive-CC classification must retain exact MCH 0x1B, SAM all-empty non-self, VPR all-empty self, DNC variation-0 all-empty self signatures, and urgent-before-DNC-before-Purify priority.'
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
    'MaximumObservedSignals = 128',
    'casterEntityId == targetEntityId',
    '(globalSequence != 0 || sourceSequence != 0)',
    'ActiveResilienceStatusCount',
    'ResiliencePresenceObserved',
    'ResilienceObservedAtMilliseconds',
    'ResilienceMissingSinceMilliseconds',
    'HasExactTeamFocus',
    'HigherPriorityClaimed',
    'ReadyForPromotion',
    'PromotionIntent',
    'ReleasedAtMilliseconds >= Signal.ObservedAtMilliseconds',
    'state.ReleasedAtMilliseconds',
    'public bool ShouldPromote',
    'RetiresSignalBeforePromotion'
) 'Exact positive-observation reactive CC post-Purify policy'
if ($normalizedMiracleCleanseFollowupRules -notmatch 'IsExactPurifySignal\(.*?IsValidEntityId\(casterEntityId\) && casterEntityId == targetEntityId && actionId == PurifyActionId && effectType == RecoveredFromStatusEffectType && IsPurifyRemovableStatus\(effectValue\) && \(globalSequence != 0 \|\| sourceSequence != 0\)' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'IsPurifyRemovableStatus\(uint statusId\) => statusId is StunStatusId or HeavyStatusId or BindStatusId or SilenceStatusId or MiracleOfNatureStatusId or DeepFreezeStatusId;' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'ValidateCandidate\(signal\.Target, observation\.Candidate\).*?value\.Target != expected \? MiracleCleanseFollowupCancelReason\.CandidateChanged') {
    throw 'The follow-up must bind one exact self-Purify recovery from exactly six reviewed CC statuses to one unchanged exact canonical actor.'
}
if ($normalizedMiracleCleanseFollowupRules -notmatch 'if \(age >= ResilienceAcquisitionMilliseconds\).*?ResilienceNotObserved.*?if \(candidate\.ActiveResilienceStatusCount == 0\).*?SignalObserved.*?Waiting.*?Phase = MiracleCleanseFollowupPhase\.WaitingForResilienceEnd, ResiliencePresenceObserved = true, ResilienceObservedAtMilliseconds = nowMilliseconds' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'if \(!state\.ResiliencePresenceObserved \|\| state\.ResilienceObservedAtMilliseconds < 0\).*?if \(candidate\.ActiveResilienceStatusCount == 1\).*?ResilienceMissingSinceMilliseconds = -1.*?if \(state\.ResilienceMissingSinceMilliseconds < 0\).*?ResilienceMissingSinceMilliseconds = observation\.NowMilliseconds.*?if \(missingAge < ResilienceMissingGraceMilliseconds\) return Waiting\(state\);.*?Phase = MiracleCleanseFollowupPhase\.ReleaseOpportunity') {
    throw 'Resilience must be positively observed within 750 ms before 150 ms of continuous live absence can open a release opportunity.'
}
if ($normalizedMiracleCleanseFollowupRules -notmatch 'var age = observation\.NowMilliseconds - state\.ResilienceObservedAtMilliseconds; if \(age < 0\).*?ClockMovedBackwards.*?if \(age >= ResilienceReleaseWaitMilliseconds\).*?ResilienceReleaseTimedOut.*?if \(candidate\.ActiveResilienceStatusCount == 1\)' -or
    $normalizedMiracleCleanseFollowupRules -notmatch 'var releaseAge = observation\.NowMilliseconds - state\.ReleasedAtMilliseconds;.*?if \(releaseAge >= ReleaseOpportunityMilliseconds\).*?ReleaseOpportunityExpired.*?if \(observation\.HigherPriorityClaimed\) return Waiting\(state\);.*?if \(!observation\.HasExactTeamFocus\) return Waiting\(state\);.*?new MiracleCleanseFollowupIntent\( signal, state\.ReleasedAtMilliseconds\).*?ReadyForPromotion.*?intent') {
    throw 'The 3-second hard release deadline and positive Resilience 3248 observation must precede stable absence; the 500-ms promotion window then requires exact team focus and yields to urgent threats without extension.'
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
    'defensiveUtilityHeldEnabled',
    'defensiveUtilityHeldWasEnabled',
    'allyRescueHeldEnabled',
    'miracleInterceptHeldEnabled',
    'miracleInterceptHeldWasEnabled',
    'heldOptionJustEnabled',
    'probe.Reset()'
) 'Shared Purify, defensive utility, Ally Rescue, and reactive-CC input ownership'
if ($emergencyInputCoordinator -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|Hook<|HookFromAddress|ITargetManager|TargetManager)\b') {
    throw 'The shared emergency input coordinator may only observe and consume physical generations.'
}

$personalStatus = Read-RequiredSource $personalStatusPath 'Personal status coordinator'
$normalizedPersonalStatus = $personalStatus -replace '\s+', ' '
$purifyObserve = [regex]::Match($personalStatus, '\bemergencyPurify\.Observe\s*\(')
$defenseObserve = [regex]::Match($personalStatus, '\bdefensiveUtility\.Observe\s*\(')
$rescueObserve = [regex]::Match($personalStatus, '\ballyRescue\.Observe\s*\(')
$miracleObserve = [regex]::Match($personalStatus, '\bmiracleIntercept\.Observe\s*\(')
$ninjaSeitonObserve = [regex]::Match($personalStatus, '\bninjaSeiton\.Observe\s*\(')
$monkEarthReplyObserve = [regex]::Match($personalStatus, '\bmonkEarthReply\.Observe\s*\(')
if (-not $purifyObserve.Success -or -not $defenseObserve.Success -or -not $rescueObserve.Success -or
    -not $miracleObserve.Success -or -not $ninjaSeitonObserve.Success -or -not $monkEarthReplyObserve.Success -or
    $purifyObserve.Index -gt $defenseObserve.Index -or
    $defenseObserve.Index -gt $rescueObserve.Index -or
    $rescueObserve.Index -gt $miracleObserve.Index -or
    $miracleObserve.Index -gt $ninjaSeitonObserve.Index -or
    $ninjaSeitonObserve.Index -gt $monkEarthReplyObserve.Index -or
    [regex]::Matches($personalStatus, '\bemergencyInputFrame\b').Count -lt 6) {
    throw 'Personal status coordination must give Purify, defense, Ally Rescue, reactive CC, NIN Seiton, then Monk first-to-last claim on one shared input frame.'
}
Assert-Literals $personalStatus @(
    'purifyClaimedPriority',
    'defensiveUtilityClaimedPriority',
    'defensiveUtilitiesConfigurationEnabled',
    'configuration.EnableDefensiveUtilities',
    'configuration.GuardOnStunPressure',
    'configuration.PreGuardOnLowHpPressure',
    'configuration.PaladinGuardianLowAlly',
    'purify.UseActionAttempted',
    'resilienceActive',
    'guardActive',
    'EmergencyActionPriorityRules.AllyRescueClaimsPriority(',
    'miracleInterceptConfigurationEnabled,',
    '!purifyClaimedPriority &&',
    '!defensiveUtilityClaimedPriority &&',
    '!allyRescueClaimedPriority',
    'metadata.AllyRescueStatusesVerified',
    'metadata.MiracleOfNatureActionVerified',
    'metadata.MarksmanSpiteVerified',
    'metadata.ZantetsukenVerified',
    'metadata.FuriousBacklashVerified',
    'configuration.EnableReactiveCcUtilities',
    'configuration.ReactiveCcOnHeldKey',
    'configuration.ReactiveCcDancerLimitBreak',
    'configuration.ReactiveCcAfterEnemyPurify',
    'configuration.EnableNinjaSeitonOnFreshGameplayKey',
    'ninjaSeitonConfigurationEnabled',
    'new NinjaSeitonDispatchProbe(',
    'NinjaSeitonDispatchProbeSnapshot NinjaSeitonDiagnostics',
    'ninjaSeiton.FailClosed()',
    'ninjaSeiton.Reset()',
    'metadata.SeitonVerified',
    'ninjaSeiton.Observe(',
    'ninja.InputClaimed',
    'metadata.PurifyVerified',
    'context == SupportedPvPContext.CrystallineConflict'
) 'Shared self-Purify, defensive utility, Ally Rescue, reactive-CC, and NIN Seiton priority'
if ($normalizedPersonalStatus -notmatch 'miracleIntercept\.Observe\( localPlayer, isCrystallineConflict, miracleInterceptConfigurationEnabled, configuration\.ReactiveCcOnHeldKey, !purifyClaimedPriority && !defensiveUtilityClaimedPriority && !allyRescueClaimedPriority,') {
    throw 'Reactive CC must receive persistent feature/capture enablement separately from its transient Purify/defense/Rescue dispatch permission.'
}
if ($normalizedPersonalStatus -notmatch 'configuration\.MiracleInterceptMchLimitBreak, configuration\.MiracleInterceptSamZantetsuken, configuration\.MiracleInterceptViperNest, configuration\.ReactiveCcDancerLimitBreak, configuration\.ReactiveCcAfterEnemyPurify, metadata\.MarksmanSpiteVerified, metadata\.ZantetsukenVerified, metadata\.FuriousBacklashVerified, metadata\.MiracleOfNatureActionVerified, metadata\.PurifyVerified, emergencyInputFrame') {
    throw 'Reactive MCH/SAM/VPR/DNC/Purify subtypes and metadata gates must be wired separately into the shared one-generation dispatcher.'
}
if ($normalizedPersonalStatus -notmatch 'var ninjaSeitonConfigurationEnabled = configuration\.Enabled && configuration\.EnableNinjaSeitonOnFreshGameplayKey;' -or
    $normalizedPersonalStatus -notmatch 'var ninja = ninjaSeiton\.Observe\( localPlayer, isCrystallineConflict, ninjaSeitonConfigurationEnabled, metadata\.SeitonVerified, guardActive, purifyClaimedPriority \|\| defensiveUtilityClaimedPriority \|\| allyRescueClaimedPriority \|\| miracle\.UseActionAttempted \|\| emergencyInputFrame\.IsConsumed, emergencyInputFrame') {
    throw 'NIN Seiton must remain an exact-CC, verified-metadata, Guard-suppressed fresh-key consumer after Purify/defense/Rescue/reactive CC.'
}
$normalizedEmergencyPriority = (Read-RequiredSource (Join-Path $coreRoot 'AllyRescueBufferRules.cs') 'Emergency action priority rules') -replace '\s+', ' '
if ($normalizedEmergencyPriority -notmatch 'AllowMiracleIntercept\( EmergencyPurifyBufferDecision purifyDecision, AllyRescueBufferDecision rescueDecision\)\s*=>\s*!SelfPurifyClaimsPriority\(purifyDecision\)\s*&&\s*!AllyRescueClaimsPriority\(rescueDecision\)') {
    throw 'Core emergency-action priority must permit Miracle only after both self-Purify and Ally Rescue decline the generation.'
}
if ($personalStatus -match '\bstatus\.Address\b|\bStatusAddress\b') {
    throw 'Personal status scanning must never gate on status.Address.'
}
if ($normalizedPersonalStatus -notmatch 'var guardActive = DefensiveUtilityProbe\.HasActiveGuard\(localPlayer\); var exactGuardActive = guardActive; var guardObservationNow = Math\.Max\(now, Environment\.TickCount64\); var observedGuardAttemptAt = -1L; if \(localPlayer is not null\) \{ nearAssist\.TryGetRecentExactLocalGuardAttempt\( clientState\.TerritoryType, localPlayer\.GameObjectId, localPlayer\.EntityId, guardObservationNow, DefensiveUtilityRules\.GuardPropagationLatchMilliseconds, out observedGuardAttemptAt\); \} guardActive = defensiveUtility\.ObserveGuardSuppression\( exactGuardActive, observedGuardAttemptAt, guardObservationNow, hardReset\)\.SuppressDirectActionHelpers;' -or
    $normalizedPersonalStatus -notmatch 'regularPurifyConfigurationEnabled = .*?!guardActive;.*?pressureStunPurifyConfigurationEnabled = .*?!guardActive;.*?allyRescueConfigurationEnabled = .*?!guardActive;.*?miracleInterceptConfigurationEnabled = .*?!guardActive;.*?configuration\.EnableMonkEarthReplyHelper && !guardActive') {
    throw 'Exact live or identity-and-territory-bound propagated Guard must be computed independently of the defensive-utility master and suppress every plugin-owned direct action path.'
}
if ($normalizedPersonalStatus -notmatch 'var defense = defensiveUtility\.Observe\( localPlayer, isCrystallineConflict, defensiveUtilitiesConfigurationEnabled, configuration\.DefensiveUtilitiesOnHeldKey, configuration\.GuardOnStunPressure, configuration\.PreGuardOnLowHpPressure, configuration\.PaladinGuardianLowAlly, pressureKnown, incomingEnemyCount, highPressureStunObserved, purify\.UseActionAttempted, resilienceActive, hasPurifyRemovableCrowdControl, guardActive, purifyClaimedPriority, emergencyInputFrame') {
    throw 'Defensive utilities must observe after Purify with its attempted result, positive Resilience state, active-Guard state, and Purify priority on the same shared generation.'
}

# Defensive utilities share the same physical input generation. Purify owns a
# pressured Stun first; Guard may follow only on a later generation after a
# positive Resilience observation. Guard, pre-Guard, and Guardian are one-shot.
$defensiveUtilityRules = Read-RequiredSource $defensiveUtilityRulesPath 'Defensive utility rules'
$normalizedDefensiveUtilityRules = $defensiveUtilityRules -replace '\s+', ' '
$defensiveUtility = Read-RequiredSource $defensiveUtilityProbePath 'Defensive utility runtime'
$normalizedDefensiveUtility = $defensiveUtility -replace '\s+', ' '
Assert-Literals $defensiveUtilityRules @(
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
    'PreGuardHpPercent = 50',
    'GuardianAllyHpPercent = 20',
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
) 'Exact pressure, HP, native Guardian reachability, Resilience, and one-intent defensive rules'
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
    'public static bool IsGuardianCandidate\(.*?\) \{.*?\} public static int SelectGuardianCandidateIndex').Value
if ($normalizedDefensiveUtilityRules -notmatch 'IsPreGuardRisk\(.*?\) => !guardActive && !hasPurifyRemovableCrowdControl && IsHighPressure\(pressureKnown, incomingEnemyCount\) && IsAtOrBelowHpPercent\(currentHp, maximumHp, PreGuardHpPercent\)' -or
    $normalizedDefensiveUtilityRules -notmatch 'CanDispatchPostPurifyGuard\(.*?\) => !awaitingPurifyConfirmation && resilienceObserved && !hasPurifyRemovableCrowdControl && nowMilliseconds >= 0 && expiresAtMilliseconds > nowMilliseconds' -or
    [string]::IsNullOrWhiteSpace($guardianCandidateMethod) -or
    $guardianCandidateMethod -notmatch 'candidate\.HasValidNativeTarget && candidate\.HasNativeRangeAndLineOfSight && float\.IsFinite\(candidate\.DistanceSquared\) && candidate\.DistanceSquared >= 0f && IsAtOrBelowHpPercent\( candidate\.CurrentHp, candidate\.MaximumHp, GuardianAllyHpPercent\)' -or
    $guardianCandidateMethod -match 'DistanceSquared\s*<' -or
    $defensiveUtilityRules -match '\bGuardianStrictMaximumDistance\b|\bstrictMaximumDistanceSquared\b') {
    throw 'Defensive rules must require known pressure >=3, self HP <=50%, ally HP <=20%, finite nonnegative distance, native Guardian range/LoS, and positive post-Purify Resilience without a raw-distance upper cap.'
}
if ([regex]::Matches($defensiveUtility, '(?:->|\.)UseAction\s*\(').Count -ne 2) {
    throw 'Defensive utility runtime must contain exactly one Guard and one Guardian native UseAction boundary.'
}
Assert-Literals $defensiveUtility @(
    'EnemyCombatConstants.GuardActionId',
    'EnemyCombatConstants.GuardianActionId',
    'EnemyCombatConstants.GuardianActionId',
    'purifyUseActionAttempted',
    'awaitingPostPurifyConfirmation = true',
    'resilienceActive &&',
    '!hasPurifyRemovableCrowdControl',
    'preGuardEpisodeSpent = true',
    'guardianSpentActors.Add(selected.Actor)',
    'inputFrame.Consume()',
    'nearAssist.RunWithoutRedirect',
    'ActionManager.UseActionMode.None',
    'PartySlotResolver.Resolve',
    'GetActionInRangeOrLoS',
    'SelectGuardianCandidateIndex',
    'GuardianTriggerPopup? GuardianPopup',
    'selectedGuardianPartySlot = selected.PartySlot',
    'ObserveGuardianTriggerPopup(',
    'HasActiveGuard(localPlayer)',
    'IsActionOffCooldown(EnemyCombatConstants.GuardActionId)',
    'IsActionOffCooldown(EnemyCombatConstants.GuardianActionId)',
    'will not be retried for this intent'
) 'One-generation exact Guard and Guardian runtime'
if ($normalizedDefensiveUtility -notmatch 'if \(highPressureStunObserved && purifyUseActionAttempted\) \{ awaitingPostPurifyConfirmation = true; postPurifyGuardExpiresAt = SaturatingAdd\( nowMilliseconds, DefensiveUtilityRules\.PostPurifyGuardWindowMilliseconds\); \}.*?if \(awaitingPostPurifyConfirmation && resilienceActive && !hasPurifyRemovableCrowdControl\) \{ awaitingPostPurifyConfirmation = false;' -or
    $normalizedDefensiveUtility -notmatch 'trigger = DefensiveUtilityTrigger\.PostPurifyHighPressureStun; postPurifyGuardExpiresAt = -1; awaitingPostPurifyConfirmation = false; inputClaimed = true; inputFrame\.Consume\(\); accepted = TryUseGuardOnce' -or
    $normalizedDefensiveUtility -notmatch 'trigger = DefensiveUtilityTrigger\.PreGuardLowHpPressure; preGuardEpisodeSpent = true; inputClaimed = true; inputFrame\.Consume\(\); accepted = TryUseGuardOnce' -or
    $normalizedDefensiveUtility -notmatch 'guardianSpentActors\.Add\(selected\.Actor\); inputClaimed = true; inputFrame\.Consume\(\); accepted = TryUseGuardianOnce') {
    throw 'Each defensive intent must be retired and its shared physical generation consumed before the sole native request; Purify follow-up requires a later Resilience-confirmed generation.'
}
if ([regex]::Matches($defensiveUtility, '\bObserveGuardianTriggerPopup\s*\(').Count -ne 1 -or
    $normalizedDefensiveUtility -notmatch 'action = DefensiveUtilityActionKind\.Guardian; trigger = DefensiveUtilityTrigger\.PaladinGuardianLowAlly; targetGameObjectId = selected\.GameObjectId; targetEntityId = selected\.EntityId; selectedGuardianPartySlot = selected\.PartySlot; guardianSpentActors\.Add\(selected\.Actor\); inputClaimed = true; inputFrame\.Consume\(\); accepted = TryUseGuardianOnce\(localPlayer!, selected, out attempted\);' -or
    $normalizedDefensiveUtility -notmatch 'guardianPopup = DefensiveUtilityRules\.ObserveGuardianTriggerPopup\( guardianPopup, configurationEnabled && isCrystallineConflict && enablePaladinGuardianLowAlly && localIdentityValid && IsPaladin\(localPlayer!\), action, trigger, attempted, accepted, selectedGuardianPartySlot, nowMilliseconds, hardReset\);' -or
    $normalizedDefensiveUtility -notmatch 'if \(hardReset\) ResetRuntime\(\); else if \(!configurationEnabled \|\| !isCrystallineConflict\) ResetOpportunityRuntime\(\);' -or
    $normalizedDefensiveUtility -notmatch 'private void ResetOpportunityRuntime\(\) \{.*?guardianPopup = null; \}') {
    throw 'Only the attempted-and-client-accepted PLD Guardian helper branch may feed the popup, and disable, CC-context loss, fail-closed, or reset must clear it.'
}
if ($normalizedDefensiveUtility -notmatch 'actionManager->UseAction\( ActionType\.Action, EnemyCombatConstants\.GuardActionId, localPlayer\.GameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)' -or
    $normalizedDefensiveUtility -notmatch 'actionManager->UseAction\( ActionType\.Action, EnemyCombatConstants\.GuardianActionId, ally\.GameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)') {
    throw 'Defensive native calls must be exact Action 29054 to self and exact Action 29066 to the revalidated ally.'
}
$guardUseMethod = [regex]::Match(
    $defensiveUtility,
    '(?s)private unsafe bool TryUseGuardOnce\(.*?\r?\n    \}\r?\n\r?\n    private unsafe bool TryUseGuardianOnce').Value
$normalizedGuardUseMethod = $guardUseMethod -replace '\s+', ' '
$guardAttemptCommitIndex = $normalizedGuardUseMethod.IndexOf('ObserveGuardSuppression(')
$guardNativeRequestIndex = $normalizedGuardUseMethod.IndexOf('actionManager->UseAction(')
if ([string]::IsNullOrWhiteSpace($guardUseMethod) -or
    [regex]::Matches($guardUseMethod, '\bObserveGuardSuppression\s*\(').Count -ne 1 -or
    $guardAttemptCommitIndex -lt 0 -or
    $guardNativeRequestIndex -le $guardAttemptCommitIndex -or
    $normalizedGuardUseMethod -notmatch 'attempted = true;.*?ObserveGuardSuppression\( exactGuardActive: false, observedGuardAttemptAtMilliseconds: Environment\.TickCount64, nowMilliseconds: Environment\.TickCount64\);.*?nearAssist\.RunWithoutRedirect\(\(\) => actionManager->UseAction\(') {
    throw 'Plugin-owned Guard must commit global propagation suppression after accepting the attempt but before its sole native UseAction request.'
}
if ($normalizedDefensiveUtility -notmatch 'var canDispatch = configurationEnabled && isCrystallineConflict && localIdentityValid && input\.ProbeSucceeded && !input\.IsTextInputActive && inputEligible && !guardActive && !higherPriorityClaimed;') {
    throw 'The effective live-or-propagated Guard gate must suppress Guard and Guardian dispatch inside the defensive helper itself.'
}
if ($defensiveUtility -match '(?-i:\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction)\b)|(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=|\bstatus\.Address\b') {
    throw 'Defensive utilities must never retry, custom-queue, mutate a target, or depend on status-slot addresses.'
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

# Reactive CC is the fourth direct-action boundary after Purify, defensive utility,
# and Ally Rescue. It remains CC-only, WHM/BRD-only, exact-enemy, and one-shot.
$miracleIntercept = Read-RequiredSource $miracleInterceptProbePath 'Miracle intercept probe'
$normalizedMiracleIntercept = $miracleIntercept -replace '\s+', ' '
if ([regex]::Matches($miracleIntercept, '(?:->|\.)UseAction\s*\(').Count -ne 1) {
    throw 'Reactive CC must contain exactly one native UseAction call shared by WHM and BRD.'
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
    'EnemyCombatConstants.DancerJobId',
    'EnemyCombatConstants.MiracleOfNatureActionId',
    'EnemyCombatConstants.SilentNocturneActionId',
    'EnemyCombatConstants.ContradanceActionId',
    'signal.LocalEntityId != localPlayer.EntityId',
    'EnemyCombatConstants.MachinistJobId',
    'EnemyCombatConstants.SamuraiJobId',
    'EnemyCombatConstants.ViperJobId',
    'executeTracker.Enemies',
    'HasAnyVerifiedCcProtection',
    'HasVerifiedActiveStatus',
    'CcImmunityBrakeActionCatalog.IsBlockerStatus(',
    'BlockerFamilyForAction(counterActionId)',
    'Actor status-list membership is the authoritative live presence',
    'GetActionInRangeOrLoS',
    'SeitonRangeRules.HasNativeRangeAndLineOfSight',
    'HasExactTeamFocus(',
    '((Character*)localPlayer.Address)->GetTargetId()',
    'pressureTracker.GetTeamTargetCount(',
    'return alliedTargetCount >= 1',
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
    'MiracleInterceptThreatKind.PostPurifyCrowdControl',
    'MiracleCleanseFollowupRules.ReleaseOpportunityMilliseconds'
) 'Bounded exact-target WHM/BRD reactive-CC runtime'
if ($normalizedMiracleIntercept -notmatch 'counterActionId = ResolveCounterActionId\( localJobId, miracleMetadataVerified, silentNocturneMetadataVerified\); var protectionMetadataReady = RequiredProtectionStatusIds\(counterActionId\)\.All\( verifiedProtectionStatusIds\.Contains\); var enabled = configurationEnabled && isCrystallineConflict && localIdentityValid && counterActionId != 0 && protectionMetadataReady;' -or
    $normalizedMiracleIntercept -notmatch 'var contradanceEnabled = enableContradance && contradanceMetadataVerified; var cleanseSignals = DrainThreats\( localPlayer!, marksmanSpiteEnabled, zantetsukenEnabled, furiousBacklashEnabled, contradanceEnabled, cleanseFollowupEnabled, nowMilliseconds\)' -or
    $miracleIntercept -match '\bShowCcProtection\b') {
    throw 'Reactive CC must require exact WHM/BRD action metadata, its action-specific blocker metadata, and each independently verified threat before arming.'
}
if ($normalizedMiracleIntercept -notmatch 'var cleanseFollowupEnabled = enabled && enablePostPurifyCrowdControl && purifyMetadataVerified;' -or
    $normalizedMiracleIntercept -notmatch 'capture\.SetMiracleCleanseFollowupLocalEntityId\( cleanseFollowupEnabled && localAlive \? localPlayer!\.EntityId : 0\)') {
    throw 'Post-Purify CC capture must remain separately gated by its toggle, verified Purify metadata, live WHM/BRD identity, and CC-only master.'
}
if ($normalizedMiracleIntercept -notmatch 'private IPlayerCharacter\? ResolveCleanseFollowupCandidate\( IPlayerCharacter localPlayer, MiracleCleanseFollowupTargetIdentity target\).*?enemy\.GameObjectId == target\.GameObjectId && enemy\.EntityId == target\.EntityId && enemy\.JobId == target\.JobId.*?Take\(2\).*?if \(canonical\.Length != 1\) return null;.*?player\.GameObjectId == target\.GameObjectId && player\.EntityId == target\.EntityId && player\.GameObjectId != localPlayer\.GameObjectId && player\.ClassJob\.IsValid && player\.ClassJob\.RowId == target\.JobId.*?Take\(2\).*?return players\.Length == 1 && IsLivePlayer\(players\[0\]\) && HasValidNativeIdentity\(players\[0\]\)') {
    throw 'Post-Purify status observation must resolve exactly one unchanged canonical e1-e5 and exactly one matching live native player actor.'
}
if ($normalizedMiracleIntercept -notmatch 'var blockerFamily = BlockerFamilyForAction\(counterActionId\); var anyProtection = HasAnyVerifiedCcProtection\(candidate, blockerFamily\);.*?var teamFocus = threat\.Kind != MiracleInterceptThreatKind\.PostPurifyCrowdControl \|\| HasExactTeamFocus\(localPlayer!, candidate, out cleanseFollowupTeamPressure\); var locallyReady = !hardenedScales && !otherProtection && rangeAndLineOfSight && teamFocus' -or
    $normalizedMiracleIntercept -notmatch 'var revalidatedProtection = revalidated is not null && HasAnyVerifiedCcProtection\(revalidated, blockerFamily\);.*?var revalidatedTeamFocus = revalidated is not null && \(threat\.Kind != MiracleInterceptThreatKind\.PostPurifyCrowdControl \|\| HasExactTeamFocus\(localPlayer!, revalidated, out cleanseFollowupTeamPressure\)\); if \(revalidated is not null && !revalidatedHardened && !revalidatedProtection && revalidatedRange && revalidatedTeamFocus\)') {
    throw 'Reactive CC must revalidate its action-specific blocker family, range/LoS, and exact local-plus-one-ally focus for post-Purify before spending the one action.'
}
if ($normalizedMiracleIntercept -notmatch 'var targetId = \(\(Character\*\)localPlayer\.Address\)->GetTargetId\(\); if \(targetId\.Id != candidate\.GameObjectId \|\| targetId\.ObjectId != candidate\.EntityId\) \{ return false; \} var alliedTargetCount = pressureTracker\.GetTeamTargetCount\( candidate\.GameObjectId, candidate\.EntityId\); totalTargetCount = 1 \+ Math\.Max\(0, alliedTargetCount\); return alliedTargetCount >= 1;') {
    throw 'Post-Purify reactive CC must prove the exact local native hard target plus at least one separate allied hard target on the same actor.'
}
if ($normalizedMiracleIntercept -notmatch 'ResolveCounterActionId\(.*?EnemyCombatConstants\.WhiteMageJobId when miracleMetadataVerified => EnemyCombatConstants\.MiracleOfNatureActionId, EnemyCombatConstants\.BardJobId when silentNocturneMetadataVerified => EnemyCombatConstants\.SilentNocturneActionId' -or
    $normalizedMiracleIntercept -notmatch 'if \(MiracleInterceptConfirmationRules\.ExpectedStatusForAction\(actionId\) == 0 \|\| !TargetHighlightRules\.IsValidGameObjectId\(targetGameObjectId\)\).*?actionManager->UseAction\( ActionType\.Action, actionId, targetGameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)') {
    throw 'Reactive CC may resolve only WHM 29228 or BRD 29395 and issue that exact action once to the revalidated enemy.'
}
$miracleConsumeState = [regex]::Match($miracleIntercept, 'activeThreat\s*=\s*null\s*;\s*\r?\n\s*inputFrame\.Consume\s*\(\s*\)\s*;')
$miracleTryUse = [regex]::Match($miracleIntercept, '\bTryUseCounterCcOnce\s*\(\s*counterActionId\s*,\s*revalidated\.GameObjectId')
$miracleNativeCall = [regex]::Match($miracleIntercept, 'actionManager->UseAction\s*\(')
if (-not $miracleConsumeState.Success -or -not $miracleTryUse.Success -or -not $miracleNativeCall.Success -or
    $miracleConsumeState.Index -gt $miracleTryUse.Index -or
    $miracleTryUse.Index -gt $miracleNativeCall.Index) {
    throw 'Reactive CC must spend its threat and shared input before its one revalidated native action attempt.'
}
if ($miracleIntercept -match '\b(GetAdjustedActionId|GetActionStatus|IsActionOffCooldown|AnimationLock|CurrentMp|CurrentMount|CanUseActionOnTarget)\b' -or
    $miracleIntercept -match '(?-i:\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction)\b)' -or
    $miracleIntercept -match '(?-i:\bTargetManager\b)|\bITargetManager\b|\bSetTarget\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Reactive CC must never cooldown-prefilter, retry, queue, or mutate a visible target.'
}
if ($miracleIntercept -match '\bstatus\.RemainingTime\b|\bstatus\.[A-Za-z_]*Address\b|\b(StatusAddress|StatusInstanceToken)\b' -or
    $normalizedMiracleIntercept -notmatch 'foreach \(var status in player\.StatusList\).*?verifiedProtectionStatusIds\.Contains\(status\.StatusId\) && CcImmunityBrakeActionCatalog\.IsBlockerStatus\( blockerFamily, status\.StatusId, targetJobId\).*?return true' -or
    $normalizedMiracleIntercept -notmatch 'foreach \(var status in player\.StatusList\).*?status\.StatusId == statusId.*?return true' -or
    $normalizedMiracleIntercept -notmatch 'private static int CountActiveStatuses\(IPlayerCharacter player, uint statusId\).*?foreach \(var status in player\.StatusList\).*?if \(status\.StatusId != statusId\) continue; count\+\+; if \(count > 1\) return count;') {
    throw 'Reactive-CC protection and Resilience-release gates must use unambiguous live StatusList membership, never status addresses or RemainingTime prediction.'
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
    'AddStatusEffectType = 0x0E',
    'CorrelationMilliseconds = 1_500',
    'PopupDurationMilliseconds = 1_500',
    'MaximumConfirmedKeys = 128',
    'MiracleInterceptThreatKind.MarksmanSpite',
    'MiracleInterceptThreatKind.Zantetsuken',
    'MiracleInterceptThreatKind.FuriousBacklash',
    'MiracleInterceptThreatKind.Contradance',
    'MiracleInterceptThreatKind.PostPurifyCrowdControl',
    'observation.CasterEntityId == pending.LocalCasterEntityId',
    'observation.ActionId == pending.ActionId',
    'observation.TargetEntityId == pending.TargetEntityId',
    'observation.EffectType == AddStatusEffectType',
    'observation.EffectValue == ExpectedStatusForAction(pending.ActionId)',
    'observation.GlobalSequence != 0 || observation.SourceSequence != 0',
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
    $normalizedMiracleConfirmationRules -notmatch 'if \(PendingInsideWindow\(previous\.Pending, nowMilliseconds\) is \{ \} activePending\) \{ return None\(previous with \{ Pending = activePending, Popup = ActivePopup\(previous\.Popup, nowMilliseconds\), LastObservedAtMilliseconds = nowMilliseconds, \}\); \} if \(!attempt\.IsValid \|\| attempt\.AttemptedAtMilliseconds != nowMilliseconds\).*?Pending = attempt') {
    throw 'Reactive-CC landing correlation must be forward-only within 1500 ms, preserve the first active pending attempt, and deduplicate through a bounded 128-key history.'
}
if ($miracleConfirmationRules -match '\b(UseAction|UseActionLocation|ITargetManager|TargetManager|SetTarget|SendInput|keybd_event|mouse_event)\b') {
    throw 'Reactive-CC landing confirmation rules must remain observational and never initiate actions, input, or target changes.'
}
if ($normalizedMiracleConfirmationRules -notmatch 'ExpectedStatusForAction\(uint actionId\) => actionId switch \{ MiracleOfNatureActionId => MiracleOfNatureStatusId, SilentNocturneActionId => SilenceStatusId, _ => 0, \}' -or
    $normalizedMiracleConfirmationRules -notmatch 'observation\.ActionId == pending\.ActionId.*?observation\.EffectType == AddStatusEffectType.*?observation\.EffectValue == ExpectedStatusForAction\(pending\.ActionId\)') {
    throw 'AddStatus 0x0E confirmation must correlate WHM 29228 to 3085 and BRD 29395 to 1347 by the exact attempted action.'
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
    'Waiting for exact reactive-CC landing evidence'
) 'Reactive-CC landing runtime correlation and diagnostics'
$miracleRegisterIndex = $normalizedMiracleIntercept.IndexOf('MiracleInterceptConfirmationRules.RegisterAttempt(')
$miracleTryUseIndex = $normalizedMiracleIntercept.IndexOf('TryUseCounterCcOnce( counterActionId, revalidated.GameObjectId')
if ($miracleTryUseIndex -lt 0 -or $miracleRegisterIndex -le $miracleTryUseIndex -or
    $normalizedMiracleIntercept -notmatch 'if \(attempted && revalidated is not null && attemptedAtMilliseconds >= 0\) \{ var registered = MiracleInterceptConfirmationRules\.RegisterAttempt') {
    throw 'Reactive-CC confirmation may register only after the sole native attempt against the revalidated exact target.'
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
    $normalizedMiracleIntercept -notmatch 'if \(!localAlive\).*?if \(confirmationPendingForLocalCaster\) DrainConfirmations\(nowMilliseconds\);.*?"Waiting for exact reactive-CC landing evidence"') {
    throw 'Every follow-up frame must run before the sole dispatch decision; urgent/helper priority may only retain it inside its original TTL, while local death must preserve exact pending landing evidence.'
}
if ([regex]::Matches($normalizedMiracleIntercept, 'ObserveCleanseFollowup\( localPlayer!, cleanseFollowupEnabled, !dispatchAllowed \|\| activeThreat is not null,').Count -ne 2) {
    throw 'Both new-signal and ordinary-frame follow-up observations must yield to urgent MCH/SAM/VPR/DNC threats and higher-priority Purify/defense/Rescue input ownership.'
}
if ($normalizedMiracleIntercept -notmatch 'cleanseFollowupState = decision\.NextState;.*?if \(!decision\.ShouldPromote \|\| decision\.PromotionIntent is not \{ \} promotion\) return;.*?if \(activeThreat is not null\).*?return;.*?activeThreat = new MiracleThreatState\( MiracleInterceptThreatKind\.PostPurifyCrowdControl, promotion\.Target\.GameObjectId, promotion\.Target\.EntityId, promotion\.Target\.JobId, promotion\.ReleasedAtMilliseconds' -or
    $normalizedMiracleIntercept -notmatch 'private static long ThreatLifetime\(MiracleInterceptThreatKind kind\) => kind == MiracleInterceptThreatKind\.PostPurifyCrowdControl \? MiracleCleanseFollowupRules\.ReleaseOpportunityMilliseconds : MiracleInterceptRules\.GetThreatLifetimeMilliseconds\(kind\);') {
    throw 'The exact post-Purify state must be retired before promotion, and the shared dispatcher must measure its unextended 500 ms from the original verified release edge.'
}
if ($normalizedMiracleIntercept -match 'MiracleInterceptThreatKind\.PostPurifyCrowdControl,.*?decision\.NextState\.LastObservedAtMilliseconds') {
    throw 'Priority-delayed post-Purify promotion must never restart its 500-ms TTL from the later framework decision time.'
}
$miraclePriorityBranch = [regex]::Match(
    $normalizedMiracleIntercept,
    'if \(!dispatchAllowed\) \{(?<Body>.*?)\} var candidate = ResolveCandidate')
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
Assert-Literals $overlaySource @(
    'miracle.ConfirmationPopup is { } miraclePopup && miraclePopup.IsVisible(now)',
    'DrawMiracleInterceptConfirmationCard(',
    '"AUTO CC LANDED"',
    '? "SILENCE"',
    ': "MIRACLE"',
    'MiracleInterceptThreatKind.MarksmanSpite => $"{action}  •  MCH LB START"',
    'MiracleInterceptThreatKind.Zantetsuken => $"{action}  •  SAM LB START"',
    'MiracleInterceptThreatKind.FuriousBacklash => $"{action}  •  VPR NEST START"',
    'MiracleInterceptThreatKind.Contradance => $"{action}  •  DNC LB START"',
    'MiracleInterceptThreatKind.PostPurifyCrowdControl =>',
    '$"{action}  •  AFTER PURIFY ({PurifyStatusLabel(popup.RemovedStatusId)})"',
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
    '$"P{popup.PartySlot}  •  CLIENT ACCEPTED"'
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
    $guardianCardBody -notmatch 'EnemyCombatConstants\.GuardianIconId.*?"GUARDIAN TRIGGERED".*?\$"P\{popup\.PartySlot\} • CLIENT ACCEPTED"' -or
    $guardianCardBody -match '(?i)\b(landed|saved|protected)\b' -or
    $guardianCardBody -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|HookFromAddress|ITargetManager|TargetManager|SetTarget|Replay|Retry|Dispatch)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=') {
    throw 'Guardian popup must remain one visual-only card in DrawPersonalWarnings, use the Guardian icon, and state only GUARDIAN TRIGGERED / P# CLIENT ACCEPTED without a server-landed or protection-success claim.'
}

# The NIN Seiton helper is a separate default-off fresh-edge action boundary.
# Pure rules select exactly one canonical CC enemy by exact HP ratio; runtime
# consumes the shared generation before revalidating only that frozen intent.
$ninjaSeitonRules = Read-RequiredSource $ninjaSeitonDispatchRulesPath 'NIN Seiton dispatch rules'
$ninjaSeiton = Read-RequiredSource $ninjaSeitonProbePath 'NIN Seiton dispatch runtime'
$normalizedNinjaSeitonRules = $ninjaSeitonRules -replace '\s+', ' '
$normalizedNinjaSeiton = $ninjaSeiton -replace '\s+', ' '
Assert-Literals $ninjaSeitonRules @(
    'BaseActionId = 29_515',
    'FollowUpActionId = 29_516',
    'IReadOnlyList<NinjaSeitonDispatchCandidate>? Candidates',
    'FreshGameplayKeyPressed',
    'ActionHelpersSuppressedByGuard',
    'HigherPriorityClaimed',
    'ExactCanonicalIdentity',
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
if ($normalizedNinjaSeitonRules -notmatch 'if \(!observation\.ConfigurationEnabled\).*?ConfigurationDisabled.*?if \(!observation\.IsCrystallineConflict\).*?OutsideCrystallineConflict.*?if \(!observation\.LocalPlayer\.IsValid\).*?LocalPlayerIdentityInvalid.*?if \(!observation\.IsLocalPlayerAlive\).*?LocalPlayerDead.*?if \(!ExecuteThreshold\.IsNinja\(observation\.LocalJobId\)\).*?LocalJobInvalid.*?if \(!observation\.MetadataVerified\).*?MetadataUnverified.*?if \(observation\.ActionHelpersSuppressedByGuard\).*?GuardSuppressed.*?if \(observation\.HigherPriorityClaimed\).*?HigherPriorityClaimed.*?if \(!observation\.InputProbeSucceeded\).*?InputProbeUnavailable.*?if \(observation\.IsTextInputActive\).*?TextInputActive.*?if \(!observation\.FreshGameplayKeyPressed\).*?NoFreshGameplayKey.*?if \(!IsExactSeitonAction\(observation\.ResolvedActionId\)\).*?ResolvedActionInvalid.*?if \(!observation\.ActionLocallyReady\).*?ActionNotReady') {
    throw 'NIN Seiton policy must require default-off enablement, exact CC/NIN/local identity, verified metadata, no Guard or higher claim, one fresh non-text key edge, and exact ready 29515/29516.'
}
if ($normalizedNinjaSeitonRules -notmatch 'candidate\.Actor != localPlayer.*?EnemySlotRules\.IsValidSlot\(candidate\.EnemySlot\).*?candidate\.ExactCanonicalIdentity.*?candidate\.Alive.*?candidate\.Targetable.*?ExecuteThreshold\.IsBelowHalf\(candidate\.CurrentHp, candidate\.MaximumHp\).*?candidate\.HasValidActionTarget.*?candidate\.HasNativeRangeAndLineOfSight' -or
    $normalizedNinjaSeitonRules -notmatch 'if \(!occupiedSlots\.Add\(candidate\.EnemySlot\) \|\| !occupiedActors\.Add\(candidate\.Actor\)\) \{ return -1; \}.*?if \(bestIndex < 0 \|\| Compare\(candidate, candidates\[bestIndex\]\) < 0\) bestIndex = index;' -or
    $normalizedNinjaSeitonRules -notmatch '\(\(ulong\)leftCurrent \* rightMaximum\)\.CompareTo\( \(ulong\)rightCurrent \* leftMaximum\)') {
    throw 'NIN Seiton selection must fail closed on duplicate exact slots/actors and rank only eligible sub-50 targets by overflow-safe exact HP ratio.'
}
if ($normalizedNinjaSeitonRules -notmatch 'var health = CompareRatio\( left\.CurrentHp, left\.MaximumHp, right\.CurrentHp, right\.MaximumHp\); if \(health != 0\) return health; var slot = left\.EnemySlot\.CompareTo\(right\.EnemySlot\); if \(slot != 0\) return slot; var entity = left\.Actor\.EntityId\.CompareTo\(right\.Actor\.EntityId\); return entity != 0 \? entity : left\.Actor\.GameObjectId\.CompareTo\(right\.Actor\.GameObjectId\);' -or
    $normalizedNinjaSeitonRules -notmatch 'intent\.IsValid && actionLocallyReady && resolvedActionId == intent\.ActionId && candidate\.EnemySlot == intent\.EnemySlot && candidate\.Actor == intent\.Target && IsEligibleCandidate\(candidate, localPlayer\)') {
    throw 'NIN Seiton must use ratio, S-slot, EntityId, and GameObjectId ordering, then validate only the frozen action/slot/actor intent.'
}
if ($ninjaSeitonRules -match '\b(UseAction|UseActionLocation|ExecuteAction|SendAction|ITargetManager|TargetManager|SetTarget|ResolvePlaceholder|Environment\.TickCount64|DateTime|Stopwatch|Task|Timer|Thread)\b' -or
    $ninjaSeitonRules -cmatch '\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction|HeldGameplayKeyEligible|PendingDispatch|BufferedDispatch)\b') {
    throw 'Pure NIN Seiton rules must never dispatch, observe time/held level, buffer, queue, retry, mutate, or depend on the visible target.'
}

if ([regex]::Matches($ninjaSeiton, '(?:->|\.)UseAction\s*\(').Count -ne 1) {
    throw 'NIN Seiton runtime must contain exactly one native UseAction boundary.'
}
Assert-Literals $ninjaSeiton @(
    'NinjaSeitonDispatchProbeSnapshot(',
    'UseActionAttempted',
    'UseActionAccepted',
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
    'inputFrame.FreshGameplayKeyPressed',
    'NinjaSeitonDispatchRules.Observe(',
    'decision.ShouldConsumeInputGeneration',
    'inputFrame.Consume()',
    'ResolveFrozenIntent(localPlayer!, intent, finalResolvedActionId)',
    'NinjaSeitonDispatchRules.CanUseExactIntent(',
    'TryUseSeitonOnce(localPlayer!, intent, out attempted)',
    'nearAssist.RunWithoutRedirect',
    'ActionType.Action',
    'ActionManager.UseActionMode.None',
    'attempted (accepted={accepted})',
    'failed and will not be retried'
) 'Exact one-attempt NIN Seiton runtime and truthful diagnostics'
if ($normalizedNinjaSeiton -notmatch 'var featureContextReady = configurationEnabled && isCrystallineConflict && localAlive && ExecuteThreshold\.IsNinja\(localJobId\) && metadataVerified && !actionHelpersSuppressedByGuard && !hardReset; var resolvedActionId = 0u; var actionReady = featureContextReady && localIdentity\.IsValid && SeitonReadinessProbe\.TryGetReadyAction\(localPlayer!, out resolvedActionId\);' -or
    $normalizedNinjaSeiton -notmatch 'var shouldResolveCandidates = actionReady && !higherPriorityClaimed && input\.ProbeSucceeded && !input\.IsTextInputActive && inputFrame\.FreshGameplayKeyPressed;.*?var candidates = shouldResolveCandidates \? ResolveExactCandidates\(localPlayer!, resolvedActionId, out candidateResolution\) : \[\];.*?FreshGameplayKeyPressed, resolvedActionId, actionReady, candidates, hardReset') {
    throw 'NIN Seiton may capture candidates only behind exact CC/NIN/metadata/Guard/readiness gates and one unclaimed fresh non-text key edge.'
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
if ($normalizedNinjaSeiton -notmatch 'foreach \(var \(slot, player\) in currentSlots\).*?var stablePlayer = EnemySlotResolver\.Resolve\(objectTable, slot\); if \(!HasValidNativeIdentity\(stablePlayer\) \|\| stablePlayer!\.Address != player\.Address \|\| stablePlayer\.GameObjectId != player\.GameObjectId \|\| stablePlayer\.EntityId != player\.EntityId\).*?return \[\];.*?resolution = \$"Exact coherent set: \{candidates\.Count\} candidates"; return candidates;') {
    throw 'NIN Seiton must re-resolve the complete native e1-e5 identity set unchanged before returning any ranked candidates.'
}
if ($normalizedNinjaSeiton -notmatch 'var target = EnemySlotResolver\.Resolve\(objectTable, enemySlot\); if \(!HasValidNativeIdentity\(target\) \|\| target!\.GameObjectId != expectedTarget\.GameObjectId \|\| target\.EntityId != expectedTarget\.EntityId\).*?var tableTarget = objectTable\.SearchByEntityId\(target\.EntityId\) as IPlayerCharacter; var exactCanonicalIdentity = tableTarget is not null && tableTarget\.Address == target\.Address && tableTarget\.GameObjectId == target\.GameObjectId && tableTarget\.EntityId == target\.EntityId;.*?SeitonReadinessProbe\.HasRangeAndLineOfSight\( localPlayer, target, actionId, out _\)') {
    throw 'Every NIN Seiton candidate must re-resolve one canonical e-slot, match both exact actor IDs/address, and pass FFXIV native range/LoS.'
}
$ninjaConsume = [regex]::Match($ninjaSeiton, 'if \(inputClaimed\) inputFrame\.Consume\(\);')
$ninjaFrozenResolve = [regex]::Match($ninjaSeiton, 'ResolveFrozenIntent\(localPlayer!, intent, finalResolvedActionId\)')
$ninjaIntentRevalidation = [regex]::Match($ninjaSeiton, 'NinjaSeitonDispatchRules\.CanUseExactIntent\s*\(')
$ninjaTryUse = [regex]::Match($ninjaSeiton, 'TryUseSeitonOnce\(localPlayer!, intent, out attempted\)')
$ninjaNativeCall = [regex]::Match($ninjaSeiton, 'actionManager->UseAction\s*\(')
if (-not $ninjaConsume.Success -or -not $ninjaFrozenResolve.Success -or
    -not $ninjaIntentRevalidation.Success -or -not $ninjaTryUse.Success -or -not $ninjaNativeCall.Success -or
    $ninjaConsume.Index -gt $ninjaFrozenResolve.Index -or
    $ninjaFrozenResolve.Index -gt $ninjaIntentRevalidation.Index -or
    $ninjaIntentRevalidation.Index -gt $ninjaTryUse.Index -or
    $ninjaTryUse.Index -gt $ninjaNativeCall.Index) {
    throw 'NIN Seiton must consume the shared input generation before frozen-target revalidation and its sole native request.'
}
$ninjaPostConsumeWindow = $ninjaSeiton.Substring(
    $ninjaConsume.Index,
    $ninjaTryUse.Index + $ninjaTryUse.Length - $ninjaConsume.Index)
if ($ninjaPostConsumeWindow -match '\b(ResolveExactCandidates|SelectBestCandidateIndex)\s*\(' -or
    $ninjaPostConsumeWindow -match '\bexecuteTracker\.Enemies\b') {
    throw 'After input consumption NIN Seiton may revalidate only the frozen intent; it must never rerank or choose an alternate.'
}
if ($normalizedNinjaSeiton -notmatch 'BuildExactSlotCandidate\( localPlayer, actionId, intent\.EnemySlot, intent\.Target\)' -or
    $normalizedNinjaSeiton -notmatch 'actionManager->UseAction\( ActionType\.Action, intent\.ActionId, intent\.Target\.GameObjectId, 0, ActionManager\.UseActionMode\.None, 0\)') {
    throw 'NIN Seiton final validation and UseAction must retain the one frozen slot, exact actor, and exact adjusted action with no fallback.'
}
if ($ninjaSeiton -match '\b(IGameInteropProvider|Hook<|HookFromAddress|SignatureAttribute|SigScanner|ITargetManager|TargetManager|SetTarget|ResolvePlaceholder)\b|\.(Target|FocusTarget|SoftTarget|MouseOverTarget|GPoseTarget)\s*=' -or
    $ninjaSeiton -cmatch '\b(RetryAction|RetryDispatch|QueuedAction|ActionQueued|QueueAction|HeldGameplayKeyEligible|PendingDispatch|BufferedDispatch)\b' -or
    $ninjaSeiton -match '(?:->|\.)Original\s*\(') {
    throw 'NIN Seiton must use only the existing internal redirect bypass and must never hook, queue, retry, mutate, or depend on a visible target.'
}
Assert-Literals $pluginSource @(
    'personalStatus.NinjaSeitonDiagnostics',
    '[Seiton Sense] ninja-seiton[decision={ninja.Decision},reason={ninja.Reason}',
    'ready={ninja.LocallyReady},action={ninja.ResolvedActionId}',
    'candidates={ninja.CandidateCount},S={ninja.EnemySlot}',
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
if (-not $monkObserve.Success -or $monkObserve.Index -lt $ninjaSeitonObserve.Index -or
    $normalizedPersonalStatus -notmatch 'var isSupportedPvPContext = context != SupportedPvPContext\.None' -or
    $normalizedPersonalStatus -notmatch 'monkEarthReply\.Observe\( localPlayer, isSupportedPvPContext, configuration\.Enabled && configuration\.EnableMonkEarthReplyHelper && !guardActive, metadata\.MonkEarthReplyVerified, configuration\.MonkEarthReplyOnLowHp, configuration\.MonkEarthReplyBeforeExpiry, configuration\.MonkEarthReplyHpPercent, configuration\.MonkEarthReplyExpirySeconds, purifyClaimedPriority \|\| defense\.InputClaimed \|\| rescue\.UseActionAttempted \|\| miracle\.UseActionAttempted \|\| ninja\.InputClaimed') {
    throw 'Monk Earth Reply must run last, be suppressed by active Guard, and yield whenever Purify/defense/Rescue/reactive CC/NIN already claimed or attempted.'
}

$targetPressureTracker = Read-RequiredSource (Join-Path $pluginServicesRoot 'TargetPressureTracker.cs') 'Target pressure tracker'
$normalizedTargetPressureTracker = $targetPressureTracker -replace '\s+', ' '
if ($normalizedTargetPressureTracker -notmatch 'supportedContext == SupportedPvPContext\.CrystallineConflict && \(\(configuration\.ExperimentalAllyRescueOnNextKey && metadata\.AllyRescueStatusesVerified\) \|\| \(configuration\.EnableNearAssistMacro && configuration\.NearHelpPreferIncomingPressure\)\)') {
    throw 'Incoming ally-pressure tracking must remain CC-only, keep Ally Rescue behind verified metadata, and activate for the explicitly enabled Near Help pressure preference.'
}
if ($normalizedTargetPressureTracker -notmatch 'configuration\.EnableDefensiveUtilities \|\| \(configuration\.EnableReactiveCcUtilities && configuration\.ReactiveCcAfterEnemyPurify\) \|\| configuration\.EnableAutoEnemyFocusMark') {
    throw 'Pressure tracking must remain active for defensive, post-Purify team-focus, and automatic Attack-1 utility consumers.'
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
    $normalizedAutoEnemyFocusMark -notmatch 'TryClearOwnedOnDispose\(\).*?ownership is not \{ \} owned \|\| pending is not null.*?context != SupportedPvPContext\.CrystallineConflict \|\| !metadata\.GuardVerified \|\| !TryGetTextInputState\(out var textInputActive\) \|\| textInputActive.*?AutoEnemyFocusMarkRules\.CanClearOwnedMarker\(.*?TryExecuteClearCommand\(owned\.EnemySlot\)') {
    throw 'Dispose may issue only one best-effort owned clear after exact CC, text, slot/entity, marker, timestamp, and rate-limit revalidation.'
}
if ($normalizedAutoEnemyFocusMark -notmatch 'blockedMarkCandidate = desiredIdentity; if \(!TryExecuteMarkCommand\(desired\.Value\.EnemySlot\)\).*?lastCommandAt = now; markCommands\+\+; pending = new PendingMarkerCommand' -or
    $normalizedAutoEnemyFocusMark -notmatch 'private bool CanIssueCommand\(long now\) => now >= lastCommandAt && now - lastCommandAt >= MinimumCommandIntervalMilliseconds') {
    throw 'Attack-1 must issue at most one command per transition, never retry the same candidate transition, and rate-limit commands to at least one second.'
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
$guardAttemptObserverMatch = [regex]::Match(
    $nearAssist,
    '(?s)private void ObserveExactLocalGuardActivationAttempt\(.*?\r?\n    \}\r?\n\r?\n    private ulong TryResolveRedirect')
if (-not $guardAttemptObserverMatch.Success) {
    throw 'The exact local Guard-attempt observer could not be isolated for safety review.'
}
$guardAttemptObserver = $guardAttemptObserverMatch.Value
$normalizedGuardAttemptObserver = $guardAttemptObserver -replace '\s+', ' '
if ($normalizedUseActionDetour -notmatch 'ObserveExactLocalGuardActivationAttempt\(thisPtr, actionType, actionId\); return useActionHook!\.Original\(' -or
    $normalizedGuardAttemptObserver -notmatch 'ResolveActionId\(actionManager, actionType, actionId\) != EnemyCombatConstants\.GuardActionId.*?var local = objectTable\.LocalPlayer; if \(!IsLivePlayer\(local\) \|\| DefensiveUtilityProbe\.HasActiveGuard\(local\)\) return; var attempt = new LocalGuardActionAttempt\( clientState\.TerritoryType, local!\.GameObjectId, local\.EntityId, Environment\.TickCount64\); lock \(guardAttemptGate\) latestLocalGuardActionAttempt = attempt;' -or
    $normalizedNearAssist -notmatch 'TryGetRecentExactLocalGuardAttempt\( uint territoryId, ulong localGameObjectId, uint localEntityId, long nowMilliseconds, long maximumAgeMilliseconds, out long observedAtMilliseconds\).*?attempt\.TerritoryId != territoryId \|\| attempt\.LocalGameObjectId != localGameObjectId \|\| attempt\.LocalEntityId != localEntityId.*?nowMilliseconds - attempt\.ObservedAtMilliseconds >= maximumAgeMilliseconds.*?observedAtMilliseconds = attempt\.ObservedAtMilliseconds; return true;') {
    throw 'The detour must observe exact Guard 29054 immediately before its sole Original and expose it only to the same live local identity in the same territory within the bounded age.'
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
if ([regex]::Matches($nearAssist, 'if\s*\(!bypassRedirect\s*&&').Count -ne 4) {
    throw 'Plugin-owned direct helper calls must bypass legacy Far Help suppression plus the Near Assist, Near Help, and Far Help branches without consuming any macro token.'
}
if ([regex]::Matches($nearAssist, 'if\s*\(!bypassRedirect\s*\)').Count -ne 0) {
    throw 'The redirect bypass may guard only the four redirect branches; it must never wrap or skip the unconditional final CC brake.'
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
    'All jobs: Defensive utilities',
    'DrawDefensiveUtilityControls()',
    'All jobs: Team-visible enemy focus sign',
    'DrawAutoEnemyFocusMarkControls()',
    '"NINJA"',
    'Seiton on fresh gameplay key (experimental)',
    'configuration.EnableNinjaSeitonOnFreshGameplayKey',
    'Default off and exact Crystalline Conflict only.',
    'exact canonical S1-S5 enemies',
    'the lowest exact HP ratio wins, then stable slot/actor identity',
    'State and input are consumed before at most one native attempt.',
    'selects again, chooses an alternate, falls back, replays, or retries',
    'A client-accepted return is dispatch feedback only',
    '"MONK"',
    'DrawMonkEarthReplyControls()',
    '"BARD / WHITE MAGE"',
    'Reactive counter-CC: Silent Nocturne / Miracle of Nature',
    'DrawReactiveCcControls()',
    'Warn when no party ally is within 20y and line of sight',
    'configuration.WarnWhenIsolated',
    'configuration.EnableAutoEnemyFocusMark'
) 'Jobs quality-of-life settings organization'
if ($normalizedSettingsWindow -notmatch 'private bool DrawJobsTab\(\).*?ALL JOBS / GENERAL QUALITY OF LIFE.*?DrawResourceAuraControls\(\).*?All jobs: Defensive utilities.*?DrawDefensiveUtilityControls\(\).*?All jobs: Team-visible enemy focus sign.*?DrawAutoEnemyFocusMarkControls\(\).*?"NINJA".*?Seiton on fresh gameplay key \(experimental\).*?configuration\.EnableNinjaSeitonOnFreshGameplayKey.*?"MONK".*?DrawMonkEarthReplyControls\(\).*?"BARD / WHITE MAGE".*?DrawReactiveCcControls\(\)') {
    throw 'Jobs tab must keep general defensive/marker utilities before Ninja, Monk, and BRD/WHM reactive controls in reviewable order.'
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
    'EnemyCombatConstants.GuardianActionId',
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
    'ValidateFeature("Silent Nocturne"',
    'ValidateFeature("Contradance"',
    'ValidateFeature("Zantetsuken"',
    'ValidateFeature("Furious Backlash"',
    'MiracleOfNatureActionVerified',
    'GuardianVerified',
    'SilentNocturneVerified',
    'ContradanceVerified',
    'ZantetsukenVerified',
    'FuriousBacklashVerified',
    'Forcibly transforms target',
    'preventing them from using actions other than Purify',
    'nullifies status afflictions that can be removed by Purify'
) 'Metadata guard'

$exactCombatIds = [ordered]@{
    GuardActionId = 29054
    GuardianActionId = 29066
    GuardianIconId = 9584
    PaladinJobId = 19
    GuardianRecast100ms = 300
    GuardianSheetRange = 20
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
    'defensiveUtility.Observe',
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

$projectFile = Read-RequiredSource (Join-Path $sourceRoot 'SeitonSense.Plugin\SeitonSense.Plugin.csproj') 'Plugin project'
$pluginManifest = Read-RequiredSource (Join-Path $sourceRoot 'SeitonSense.Plugin\SeitonSense.Plugin.json') 'Plugin manifest'
$repositoryIndex = Read-RequiredSource (Join-Path $resolvedRoot 'repo.json') 'Custom repository index'
Assert-Literals $projectFile @(
    '<Version>0.14.0.0</Version>',
    '<AssemblyVersion>0.14.0.0</AssemblyVersion>',
    '<FileVersion>0.14.0.0</FileVersion>'
) 'v0.14.0.0 project version'
Assert-Literals ($pluginManifest + $repositoryIndex) @(
    'Ninja Seiton cues and a default-off fresh-key helper',
    '"AssemblyVersion": "0.14.0.0"',
    'Configuration schema 19 keeps the helper off for new, upgraded, and reset settings.'
) 'v0.14.0.0 manifest and repository metadata'

$configurationPath = Join-Path $sourceRoot 'SeitonSense.Plugin\Models\PluginConfiguration.cs'
$configuration = Read-RequiredSource $configurationPath 'Plugin configuration'
$normalizedConfiguration = $configuration -replace '\s+', ' '
Assert-Literals $configuration @(
    'public int Version { get; set; } = 19',
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
    'WarnWhenIsolated = true',
    'IsolationWarningScale = 1f',
    'EnableAutoEnemyFocusMark = false',
    'Version = 19',
    'NormalizeCcBrakeSelections()',
    'IsCcBrakeJobEnabled(uint jobId)',
    'IsCcBrakeActionEnabled(uint actionId)',
    'if (actionId is 29244 or 29248)',
    'normalizedActions[29248] = gravityEnabled',
    'Math.Clamp(NearAssistMaxAllyDistance, 5f, 30f)',
    'Math.Clamp(MchLimitBreakSoundId, 1, 16)',
    'Clamp(IsolationWarningScale, 0.75f, 1.75f, 1f',
    'Clamp(CcProtectionEmblemScale, 0.75f, 1.75f, 1f',
    'Clamp(ResourceAuraIntensity, 0.1f, 1.5f, 0.8f',
    'Clamp(ResourceAuraPulseSpeed, 0.2f, 2f, 0.75f',
    'Math.Clamp(ResourceAuraHpPercent, 10, 80)',
    'Math.Clamp(ResourceAuraMpThreshold, 0, 10_000)',
    'Math.Clamp(MonkEarthReplyHpPercent, 10, 80)',
    'MonkEarthReplyExpirySeconds,',
    '0.5f,',
    '2.5f,'
) 'Schema-19 default-off NIN Seiton helper and prior configuration migration'
if ($configuration -notmatch '(?m)^\s*public bool EnableDefensiveUtilities \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool DefensiveUtilitiesOnHeldKey \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool GuardOnStunPressure \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool PreGuardOnLowHpPressure \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool PaladinGuardianLowAlly \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableReactiveCcUtilities \{ get; set; \}\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ReactiveCcOnHeldKey \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ReactiveCcDancerLimitBreak \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool ReactiveCcAfterEnemyPurify \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool WarnWhenIsolated \{ get; set; \} = true;\s*$' -or
    $configuration -notmatch '(?m)^\s*public bool EnableAutoEnemyFocusMark \{ get; set; \}\s*$') {
    throw 'Schema 17 new installations must keep action/marker masters off while defensive/reactive leaves and isolation warning defaults remain ready.'
}
if ([regex]::Matches($configuration, '\bEnableDefensiveUtilities\s*=\s*false\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bEnableReactiveCcUtilities\s*=\s*false\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bEnableAutoEnemyFocusMark\s*=\s*false\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bWarnWhenIsolated\s*=\s*true\s*;').Count -lt 1 -or
    [regex]::Matches($configuration, '\bDefensiveUtilitiesOnHeldKey\s*=\s*true\s*;').Count -lt 2 -or
    [regex]::Matches($configuration, '\bReactiveCcOnHeldKey\s*=\s*true\s*;').Count -lt 2) {
    throw 'Schema 17 migration/reset defaults must preserve opt-in action/marker masters, held-key leaves, and visible-by-default isolation.'
}
if ($configuration -notmatch '(?m)^\s*public bool NearHelpPreferIncomingPressure \{ get; set; \} = true;\s*$' -or
    [regex]::Matches($configuration, '\bNearHelpPreferIncomingPressure\s*=\s*true\s*;').Count -lt 2) {
    throw 'Schema 18 must enable the bounded Near Help pressure preference for upgrades and reset defaults while the shared helper master remains opt-in.'
}
if ($configuration -notmatch '(?m)^\s*public bool EnableNinjaSeitonOnFreshGameplayKey \{ get; set; \}\s*$' -or
    [regex]::Matches($configuration, '\bEnableNinjaSeitonOnFreshGameplayKey\s*=\s*false\s*;').Count -lt 2) {
    throw 'Schema 19 must keep the action-initiating NIN Seiton helper default-off for new, upgrading, and reset configurations.'
}
if ([regex]::Matches($configuration, '\bVersion\s*=\s*19\s*;').Count -lt 2 -or
    $normalizedConfiguration -notmatch 'if \(Version >= 19\).*?return;.*?if \(Version < 17\).*?EnableDefensiveUtilities = false;.*?EnableReactiveCcUtilities = ExperimentalMiracleInterceptOnHeldKey;.*?ReactiveCcDancerLimitBreak = false;.*?ReactiveCcAfterEnemyPurify = MiracleInterceptAfterPurifiedStun;.*?if \(Version < 18\).*?NearHelpPreferIncomingPressure = true;.*?if \(Version < 19\).*?EnableNinjaSeitonOnFreshGameplayKey = false;.*?Version = 19;') {
    throw 'Schema 19 must fast-path current settings, preserve schema-17/18 migrations, and introduce only the default-off NIN Seiton helper.'
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

Write-Host "Seiton Sense v0.14.0.0 safety contract verified across $($sourceFiles.Count) source files; Near Help permits exact self only for a resolved self-targetable action and uses trusted incoming pressure only inside the bounded non-critical health window, with missing in-window data falling back to exact HP and out-of-window unknowns ignored; one bounded ActionEffect hook calls Original exactly once and owns all reviewed queue limits; one shared physical generation enforces Purify > Guard/Guardian > Ally Rescue > WHM/BRD reactive CC > NIN Seiton > Monk priority with exact live or identity-and-territory-bound 1500ms propagated Guard suppressing every plugin-owned direct action even when the defense master is off, without Guard replay or retries; default-off exact-CC NIN Seiton requires a fresh non-text edge, verified adjusted 29515/29516 readiness, and a complete canonical e1-e5 view, ranks strict sub-50 targets by exact HP ratio then stable slot/actor identity, consumes before frozen-intent revalidation, and issues at most one client-requested action without target mutation, reranking, alternate, fallback, queue, or retry; defensive and Far Help Guardian eligibility trusts verified sheet range 20 plus native range/LoS without a raw center-distance upper cap; exact DNC variation-0 startup plus six enemy Purify recoveries require positive Resilience 3248, stable absence, and exact local-plus-one-ally focus before action-specific 29228/29395 AddStatus 0x0E confirmation can display truthful AUTO CC LANDED; isolation remains an exact-CC, exact-party, read-only native 20y/LoS warning; default-off Team Attack-1 never overwrites, mutates targets, or writes marker memory and may issue only hardcoded /mk attack1/off <e1-e5> after exact timestamp ownership gates, including owned disable/dispose cleanup. Shell-command execution remains source-level verified pending a live Crystalline Conflict test."
