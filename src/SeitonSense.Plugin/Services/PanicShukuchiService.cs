using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct PanicShukuchiDiagnostics(
    bool Enabled,
    bool MetadataVerified,
    SupportedPvPContext LastContext,
    PanicShukuchiPoint LastOrigin,
    PanicShukuchiPoint LastDestination,
    uint LastAdjustedActionId,
    long CommandCount,
    long AttemptCount,
    long AcceptedCount,
    long RejectedCount,
    long RefusedCount,
    string LastEvent)
{
    internal string ToChatLine() =>
        $"mode=immediate,enabled={Enabled},meta={MetadataVerified},context={LastContext},origin=" +
        $"{LastOrigin.X:0.00}/{LastOrigin.Y:0.00}/{LastOrigin.Z:0.00},destination=" +
        $"{LastDestination.X:0.00}/{LastDestination.Y:0.00}/{LastDestination.Z:0.00}," +
        $"adjusted={LastAdjustedActionId}," +
        $"count={CommandCount}/{AttemptCount}/{AcceptedCount}/{RejectedCount}/{RefusedCount}," +
        $"last={LastEvent}";
}

/// <summary>
/// Executes only an explicit /panicshu command. Every invocation computes one
/// exact 19.5-yalm forward ground point and immediately makes at most one native
/// UseActionLocation call. It deliberately has no scheduler, Guard/CC gate,
/// Purify priority, cast/queue/animation wait, cooldown precheck, pending state,
/// retry, shorter-point fallback, cursor movement, or target mutation.
/// </summary>
internal sealed class PanicShukuchiService
{
    internal const string Command = "/panicshu";

    private const ulong DefaultTargetSentinel = 0xE0000000UL;
    private const float GroundProbeStartAboveYalms = 5f;
    private const float GroundProbeMaximumDistanceYalms = 10f;

    private readonly object diagnosticsGate = new();
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private readonly bool metadataVerified;

    private long commandCount;
    private long attemptCount;
    private long acceptedCount;
    private long rejectedCount;
    private long refusedCount;
    private SupportedPvPContext lastContext;
    private PanicShukuchiPoint lastOrigin;
    private PanicShukuchiPoint lastDestination;
    private uint lastAdjustedActionId;
    private string lastEvent;

    internal PanicShukuchiService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        IPluginLog log,
        PvPMetadataValidation metadata)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.log = log;
        metadataVerified = metadata.PanicShukuchiVerified;
        lastEvent = metadataVerified
            ? "Ready for immediate explicit /panicshu"
            : "Metadata mismatch; disabled";
    }

    internal PanicShukuchiDiagnostics Diagnostics
    {
        get
        {
            lock (diagnosticsGate)
            {
                return new PanicShukuchiDiagnostics(
                    configuration.Enabled,
                    metadataVerified,
                    lastContext,
                    lastOrigin,
                    lastDestination,
                    lastAdjustedActionId,
                    commandCount,
                    attemptCount,
                    acceptedCount,
                    rejectedCount,
                    refusedCount,
                    lastEvent);
            }
        }
    }

    internal unsafe void Execute(string arguments)
    {
        lock (diagnosticsGate) commandCount++;

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            RecordRefused("Arguments rejected; use /panicshu without arguments");
            return;
        }

        try
        {
            var context = ResolveContext();
            var local = objectTable.LocalPlayer;
            var localValid = HasValidLocalIdentity(local) &&
                             !local!.IsDead &&
                             local.CurrentHp > 0 &&
                             local.MaxHp > 0 &&
                             local.ClassJob.IsValid;

            var actionManager = ActionManager.Instance();
            if (actionManager == null)
            {
                RecordRefused("Action manager unavailable");
                return;
            }

            // Structural identity only: do not inspect or wait for Guard, cast,
            // queue, animation lock, cooldown, resources, or any prior attempt.
            var adjustedActionId = actionManager->GetAdjustedActionId(
                PanicShukuchiRules.ActionId);

            var origin = default(PanicShukuchiPoint);
            var candidate = default(PanicShukuchiCandidate);
            if (localValid)
            {
                var position = local!.Position;
                origin = new PanicShukuchiPoint(position.X, position.Y, position.Z);
                if (PanicShukuchiRules.TryCreateForwardProbe(
                        origin,
                        local.Rotation,
                        out var forwardProbe))
                {
                    var rayOrigin = new Vector3(
                        forwardProbe.X,
                        forwardProbe.Y + GroundProbeStartAboveYalms,
                        forwardProbe.Z);
                    if (BGCollisionModule.RaycastMaterialFilter(
                            rayOrigin,
                            -Vector3.UnitY,
                            out var groundHit,
                            GroundProbeMaximumDistanceYalms) &&
                        float.IsFinite(groundHit.Distance) &&
                        groundHit.Distance >= 0f &&
                        groundHit.Distance <= GroundProbeMaximumDistanceYalms)
                    {
                        var hit = groundHit.Point;
                        candidate = new PanicShukuchiCandidate(
                            origin,
                            local.Rotation,
                            new PanicShukuchiGroundHit(
                                true,
                                new PanicShukuchiPoint(hit.X, hit.Y, hit.Z)));
                    }
                }
            }

            var decision = PanicShukuchiRules.Evaluate(
                new PanicShukuchiCommandObservation(
                    configuration.Enabled,
                    metadataVerified,
                    context,
                    configuration.EnableWolvesDenTesting,
                    localValid && local!.ClassJob.IsValid ? local.ClassJob.RowId : 0,
                    localValid,
                    adjustedActionId,
                    candidate));

            lock (diagnosticsGate)
            {
                lastContext = context;
                lastOrigin = origin;
                lastDestination = candidate.GroundHit.Position;
                lastAdjustedActionId = adjustedActionId;
            }

            if (!decision.ShouldAttempt || decision.Intent is not { } intent)
            {
                RecordRefused($"Immediate command refused: {decision.Reason}");
                return;
            }

            var destination = new Vector3(
                intent.Destination.X,
                intent.Destination.Y,
                intent.Destination.Z);

            // The user-authored command reaches its only native boundary now.
            // No state exists that could wait, expire, retry, or replay it.
            lock (diagnosticsGate) attemptCount++;
            bool accepted;
            try
            {
                accepted = actionManager->UseActionLocation(
                    ActionType.Action,
                    PanicShukuchiRules.ActionId,
                    DefaultTargetSentinel,
                    &destination,
                    0,
                    0);
            }
            catch (Exception exception)
            {
                lock (diagnosticsGate)
                {
                    rejectedCount++;
                    lastEvent = "Immediate native Shukuchi threw; command ended";
                }

                log.Error(exception, "Seiton Sense immediate Panic Shukuchi native call failed.");
                return;
            }

            lock (diagnosticsGate)
            {
                if (accepted)
                {
                    acceptedCount++;
                    lastEvent = "Immediate native Shukuchi accepted";
                }
                else
                {
                    rejectedCount++;
                    lastEvent = "Immediate native Shukuchi rejected";
                }
            }
        }
        catch (Exception exception)
        {
            lock (diagnosticsGate)
            {
                refusedCount++;
                lastEvent = "Immediate command validation failed closed";
            }

            log.Error(exception, "Seiton Sense immediate Panic Shukuchi command failed closed.");
        }
    }

    private void RecordRefused(string eventText)
    {
        lock (diagnosticsGate)
        {
            refusedCount++;
            lastEvent = eventText;
        }
    }

    private SupportedPvPContext ResolveContext()
    {
        var condition = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private static bool HasValidLocalIdentity(IPlayerCharacter? local) =>
        local is not null &&
        local.Address != nint.Zero &&
        local.IsValid() &&
        local.GameObjectId is not 0 and not DefaultTargetSentinel and not ulong.MaxValue &&
        local.EntityId is not 0 and not (uint)DefaultTargetSentinel and not uint.MaxValue;
}
