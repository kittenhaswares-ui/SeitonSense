using SeitonSense.Core;

internal static class TargetPressureSnapshotSelfTests
{
    private static readonly TargetPressureActorIdentity Local = Identity(100);

    public static void AllSourcesAreMergedAndExposed()
    {
        var mch = Identity(10);
        var ally = Identity(200);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [Enemy(mch, jobId: 31, slot: 2, hardTarget: Local, castTarget: Local)],
            [new TargetPressureSignal(
                mch,
                TargetPressureSources.RecentHarmfulAction |
                TargetPressureSources.MachinistLimitBreakEarlyMarker)],
            [Ally(ally, mch)]);

        Equal(1, snapshot.Count, "union count");
        var opponent = snapshot.Opponents[0];
        Equal(mch, opponent.Actor, "exact actor identity");
        Equal(31u, opponent.JobId, "job");
        Equal(2, opponent.CcEnemySlot!.Value, "CC slot");
        Equal(1, opponent.AllyTargetCount, "team pressure");
        True(opponent.HasSource(TargetPressureSources.HardTarget), "hard target source");
        True(opponent.HasSource(TargetPressureSources.CastTarget), "cast target source");
        True(opponent.HasSource(TargetPressureSources.RecentHarmfulAction), "harmful source");
        True(opponent.HasSource(TargetPressureSources.MachinistLimitBreakEarlyMarker), "MCH source");
        Equal(1, snapshot.HardTargetCount, "hard target diagnostic count");
        Equal(1, snapshot.CastTargetCount, "cast target diagnostic count");
        Equal(1, snapshot.RecentHarmfulActionCount, "recent action diagnostic count");
        Equal(1, snapshot.MachinistLimitBreakCount, "MCH diagnostic count");
        True(snapshot.TryGetOpponent(mch, out var exact) && exact == opponent, "exact lookup");
        False(snapshot.TryGetOpponent(mch with { GameObjectId = mch.GameObjectId + 1 }, out _), "partial identity lookup");
    }

    public static void EnemyEligibilityFailsClosed()
    {
        var validButPartialTarget = Identity(10);
        var partialLocalIdentity = Local with { GameObjectId = Local.GameObjectId + 1 };
        TargetPressureEnemyObservation[] enemies =
        [
            Enemy(Identity(1), hardTarget: Local) with { IsHostile = false },
            Enemy(Identity(2), hardTarget: Local) with { IsDead = true },
            Enemy(Identity(3), hardTarget: Local) with { IsTargetable = false },
            Enemy(Local, hardTarget: Local),
            Enemy(Local with { EntityId = 999 }, hardTarget: Local),
            Enemy(Local with { GameObjectId = 999 }, hardTarget: Local),
            Enemy(new TargetPressureActorIdentity(0, 4), hardTarget: Local),
            Enemy(new TargetPressureActorIdentity(5, 0), hardTarget: Local),
            Enemy(validButPartialTarget, hardTarget: partialLocalIdentity),
        ];
        var signals = enemies.Take(enemies.Length - 1).Select(enemy => new TargetPressureSignal(
            enemy.Actor,
            TargetPressureSources.RecentHarmfulAction));

        var snapshot = TargetPressureSnapshot.Build(Local, enemies, signals);
        Equal(0, snapshot.Count, "invalid enemies and partial target identity are omitted");
    }

    public static void AmbiguousActorIdentitiesFailClosed()
    {
        var sameGameA = Identity(10);
        var sameGameB = sameGameA with { EntityId = 11 };
        var sameEntityA = Identity(20);
        var sameEntityB = sameEntityA with { GameObjectId = sameEntityA.GameObjectId + 1 };
        var valid = Identity(30);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [
                Enemy(sameGameA, hardTarget: Local),
                Enemy(sameGameB, hardTarget: Local),
                Enemy(sameEntityA, hardTarget: Local),
                Enemy(sameEntityB, hardTarget: Local),
                Enemy(valid, hardTarget: Local),
            ],
            [new TargetPressureSignal(sameGameA, TargetPressureSources.RecentHarmfulAction)]);

        Equal(1, snapshot.Count, "only unambiguous actor remains");
        Equal(valid, snapshot.Opponents[0].Actor, "valid identity");
    }

    public static void DuplicateObservationsMergeSafely()
    {
        var actor = Identity(10);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [
                Enemy(actor, jobId: 31, slot: 2, hardTarget: Local),
                Enemy(actor, jobId: 30, slot: 3, castTarget: Local),
                Enemy(actor, jobId: 31, slot: 2, hardTarget: Local),
            ],
            [new TargetPressureSignal(
                actor,
                TargetPressureSources.RecentHarmfulAction |
                TargetPressureSources.MachinistLimitBreakEarlyMarker)]);

        Equal(1, snapshot.Count, "duplicate actor is emitted once");
        var opponent = snapshot.Opponents[0];
        Equal(0u, opponent.JobId, "conflicting job degrades to unknown");
        False(opponent.CcEnemySlot.HasValue, "conflicting slot degrades to absent");
        True(opponent.HasSource(TargetPressureSources.HardTarget), "merged hard target");
        True(opponent.HasSource(TargetPressureSources.CastTarget), "merged cast target");
        True(opponent.HasSource(TargetPressureSources.RecentHarmfulAction), "valid event source retained");
        False(opponent.HasSource(TargetPressureSources.MachinistLimitBreakEarlyMarker), "MCH marker requires exact MCH job");
    }

    public static void EventSignalsAreNarrowAndJobChecked()
    {
        var nonMachinist = Identity(10);
        var machinist = Identity(20);
        var invalidSignalSnapshot = TargetPressureSnapshot.Build(
            Local,
            [
                Enemy(nonMachinist, jobId: 30),
                Enemy(machinist, jobId: 31),
            ],
            [
                new TargetPressureSignal(nonMachinist, TargetPressureSources.MachinistLimitBreakEarlyMarker),
                new TargetPressureSignal(machinist, TargetPressureSources.HardTarget),
                new TargetPressureSignal(
                    machinist,
                    TargetPressureSources.RecentHarmfulAction | (TargetPressureSources)0x80),
            ]);
        Equal(0, invalidSignalSnapshot.Count, "invalid or mismatched event sources cannot inject pressure");

        var validSignalSnapshot = TargetPressureSnapshot.Build(
            Local,
            [
                Enemy(nonMachinist, jobId: 30),
                Enemy(machinist, jobId: 31),
            ],
            [
                new TargetPressureSignal(nonMachinist, TargetPressureSources.RecentHarmfulAction),
                new TargetPressureSignal(machinist, TargetPressureSources.MachinistLimitBreakEarlyMarker),
            ]);
        Equal(2, validSignalSnapshot.Count, "valid event signals");
        Equal(1, validSignalSnapshot.RecentHarmfulActionCount, "harmful action count");
        Equal(1, validSignalSnapshot.MachinistLimitBreakCount, "MCH marker count");
    }

    public static void AllyTargetsAreExactAndDeduplicated()
    {
        var enemyA = Identity(10);
        var enemyB = Identity(20);
        var allyA = Identity(201);
        var allyB = Identity(202);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [
                Enemy(enemyA, slot: 1, hardTarget: Local),
                Enemy(enemyB, slot: 2),
            ],
            allies:
            [
                Ally(allyA, enemyA),
                Ally(allyA, enemyA),
                Ally(allyB, enemyA),
                Ally(Identity(203), enemyB),
                Ally(Identity(204), enemyA) with { IsDead = true },
                Ally(Identity(205), enemyA) with { IsAlly = false },
                Ally(Identity(206), enemyA) with { IsTargetable = false },
                Ally(Identity(207), enemyA with { EntityId = 999 }),
                Ally(enemyA with { EntityId = 999 }, enemyA),
                Ally(enemyB with { GameObjectId = 999 }, enemyA),
                Ally(Local, enemyA),
            ]);

        Equal(1, snapshot.Count, "only enemy A pressures local player");
        Equal(2, snapshot.Opponents[0].AllyTargetCount, "unique exact allies on enemy A");
        Equal(2, snapshot.GetAllyTargetCount(enemyA), "enemy A lookup");
        Equal(1, snapshot.GetAllyTargetCount(enemyB), "non-opponent enemy B is reusable by Near Assist");
        Equal(2, snapshot.AllyTargetCounts.Count, "both exact enemy team counts are exposed");
        Equal(enemyA, snapshot.AllyTargetCounts[0].Enemy, "team counts use deterministic CC order");
        Equal(enemyB, snapshot.AllyTargetCounts[1].Enemy, "second CC enemy");
        Equal(0, snapshot.GetAllyTargetCount(enemyA with { GameObjectId = enemyA.GameObjectId + 1 }), "partial enemy identity never matches");
    }

    public static void ConflictingAllyIdentitiesFailClosed()
    {
        var enemyA = Identity(10);
        var enemyB = Identity(20);
        var conflicting = Identity(201);
        var aliasedA = Identity(202);
        var aliasedB = aliasedA with { EntityId = 203 };
        var valid = Identity(204);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [Enemy(enemyA), Enemy(enemyB)],
            allies:
            [
                Ally(conflicting, enemyA),
                Ally(conflicting, enemyB),
                Ally(aliasedA, enemyA),
                Ally(aliasedB, enemyA),
                Ally(valid, enemyB),
            ]);

        Equal(0, snapshot.GetAllyTargetCount(enemyA), "conflicting and aliased allies are omitted");
        Equal(1, snapshot.GetAllyTargetCount(enemyB), "independent exact ally remains");
    }

    public static void IncomingHardAndCastIntentFromOneEnemyCountsOnce()
    {
        var ally = Identity(201);
        var enemy = Identity(10);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [Enemy(enemy, hardTarget: ally, castTarget: ally)],
            partyAllies: [PartyAlly(ally)]);

        True(
            snapshot.TryGetIncomingAllyPressure(ally, out var count),
            "exact party ally has a known pressure observation");
        Equal(1, count, "one enemy hard-targeting and casting on the ally counts once");
        Equal(1, snapshot.IncomingAllyPressure.Count, "one exact ally is published");
        Equal(ally, snapshot.IncomingAllyPressure[0].Ally, "published exact ally identity");
    }

    public static void IncomingIntentCountsUniqueLiveEnemies()
    {
        var ally = Identity(201);
        var enemyA = Identity(10);
        var enemyB = Identity(20);
        var deadEnemy = Identity(30);
        var untargetableEnemy = Identity(40);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [
                Enemy(enemyA, hardTarget: ally),
                Enemy(enemyA, hardTarget: ally),
                Enemy(enemyB, castTarget: ally),
                Enemy(deadEnemy, hardTarget: ally) with { IsDead = true },
                Enemy(untargetableEnemy, castTarget: ally) with { IsTargetable = false },
            ],
            partyAllies: [PartyAlly(ally)]);

        True(snapshot.TryGetIncomingAllyPressure(ally, out var count), "ally pressure is known");
        Equal(2, count, "two unique live enemies pressure the ally");
    }

    public static void IncomingIntentRejectsAmbiguousAndPartialIdentities()
    {
        var ambiguousAllyA = Identity(201);
        var ambiguousAllyB = ambiguousAllyA with { EntityId = 202 };
        var validAlly = Identity(203);
        var ambiguousEnemyA = Identity(10);
        var ambiguousEnemyB = ambiguousEnemyA with { EntityId = 11 };
        var validEnemy = Identity(20);
        var partialValidAlly = validAlly with { EntityId = 999 };
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [
                Enemy(ambiguousEnemyA, hardTarget: validAlly),
                Enemy(ambiguousEnemyB, castTarget: validAlly),
                Enemy(validEnemy, hardTarget: partialValidAlly, castTarget: validAlly),
            ],
            partyAllies:
            [
                PartyAlly(ambiguousAllyA),
                PartyAlly(ambiguousAllyB),
                PartyAlly(validAlly),
                PartyAlly(new TargetPressureActorIdentity(0, 204)),
            ]);

        False(
            snapshot.TryGetIncomingAllyPressure(ambiguousAllyA, out _),
            "ambiguous party identity is unknown");
        False(
            snapshot.TryGetIncomingAllyPressure(ambiguousAllyB, out _),
            "both aliases are excluded");
        True(
            snapshot.TryGetIncomingAllyPressure(validAlly, out var count),
            "independent exact ally remains known");
        Equal(1, count, "ambiguous enemy and partial target identity cannot add pressure");
    }

    public static void IncomingPressureDistinguishesKnownZeroFromUnknown()
    {
        var ally = Identity(201);
        var absent = Identity(202);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [Enemy(Identity(10), hardTarget: Local)],
            partyAllies: [PartyAlly(ally)]);

        True(snapshot.TryGetIncomingAllyPressure(ally, out var count), "present ally has known pressure");
        Equal(0, count, "present unpressured ally has a real zero");
        False(snapshot.TryGetIncomingAllyPressure(absent, out _), "absent ally remains unknown");
        False(TargetPressureSnapshot.Empty.TryGetIncomingAllyPressure(ally, out _), "inactive empty snapshot remains unknown");
    }

    public static void OrderingIsDeterministic()
    {
        TargetPressureEnemyObservation[] enemies =
        [
            Enemy(Identity(40), slot: 0, hardTarget: Local),
            Enemy(Identity(20), slot: 2, hardTarget: Local),
            Enemy(Identity(30), slot: 1, hardTarget: Local),
            Enemy(Identity(10), slot: 0, hardTarget: Local),
        ];
        var forward = TargetPressureSnapshot.Build(Local, enemies);
        var reverse = TargetPressureSnapshot.Build(Local, enemies.Reverse());
        var expected = new[] { Identity(30), Identity(20), Identity(10), Identity(40) };

        SequenceEqual(expected, forward.Opponents.Select(opponent => opponent.Actor), "CC slots then IDs");
        SequenceEqual(
            forward.Opponents.Select(opponent => opponent.Actor),
            reverse.Opponents.Select(opponent => opponent.Actor),
            "input order cannot reorder output");
    }

    public static void DuplicateCcSlotsAreCleared()
    {
        var slotCollisionA = Identity(10);
        var slotCollisionB = Identity(20);
        var canonical = Identity(30);
        var snapshot = TargetPressureSnapshot.Build(
            Local,
            [
                Enemy(slotCollisionA, slot: 1, hardTarget: Local),
                Enemy(slotCollisionB, slot: 1, hardTarget: Local),
                Enemy(canonical, slot: 2, hardTarget: Local),
            ]);

        Equal(canonical, snapshot.Opponents[0].Actor, "unique canonical slot sorts first");
        True(snapshot.Opponents.Skip(1).All(opponent => !opponent.CcEnemySlot.HasValue), "ambiguous slot labels are removed");
    }

    public static void InvalidLocalAndNullInputAreHandled()
    {
        Equal(
            0,
            TargetPressureSnapshot.Build(default, [Enemy(Identity(10), hardTarget: Local)]).Count,
            "invalid local player");
        Throws<ArgumentNullException>(
            () => TargetPressureSnapshot.Build(Local, null!),
            "null enemy observations");
    }

    public static void NearAssistDisabledPreservesExistingSelection()
    {
        NearAssistPressureSelectionCandidate[] candidates =
        [
            PressureCandidate(10, 2f, NearAssistAllyRole.SupportOrUnknown, default, -1),
            PressureCandidate(20, 8f, NearAssistAllyRole.RangedDamage, default, -1),
        ];
        var existing = NearAssistSelectionRules.SelectBestIndex(
            candidates.Select(candidate => candidate.Ally).ToArray(),
            preferDamageRoles: true);
        var refined = NearAssistPressureSelectionRules.SelectBestIndex(
            candidates,
            preferDamageRoles: true,
            followTeamPressure: false);

        Equal(existing, refined, "disabled feature delegates exact existing behavior");
        Equal(1, refined, "existing ranged preference remains");
    }

    public static void NearAssistPressureWinsInsideNearbyWindow()
    {
        NearAssistPressureSelectionCandidate[] candidates =
        [
            PressureCandidate(10, 2f, NearAssistAllyRole.SupportOrUnknown, Identity(30), 1),
            PressureCandidate(20, 8f, NearAssistAllyRole.RangedDamage, Identity(40), 1),
            PressureCandidate(30, 6f, NearAssistAllyRole.MeleeDamage, Identity(50), 3),
        ];

        Equal(
            2,
            NearAssistPressureSelectionRules.SelectBestIndex(candidates, true, true),
            "higher exact team pressure precedes role and distance");
    }

    public static void NearAssistPressureCannotPullAcrossArena()
    {
        NearAssistPressureSelectionCandidate[] candidates =
        [
            PressureCandidate(10, 1f, NearAssistAllyRole.SupportOrUnknown, Identity(30), 0),
            PressureCandidate(20, 9.01f, NearAssistAllyRole.RangedDamage, Identity(40), 999),
        ];

        Equal(
            0,
            NearAssistPressureSelectionRules.SelectBestIndex(candidates, true, true),
            "pressure remains inside nearest plus eight yalms");
    }

    public static void NearAssistPressureTiesUseExistingOrder()
    {
        NearAssistPressureSelectionCandidate[] candidates =
        [
            PressureCandidate(30, 5f, NearAssistAllyRole.RangedDamage, Identity(50), 2),
            PressureCandidate(20, 4f, NearAssistAllyRole.RangedDamage, Identity(60), 2),
            PressureCandidate(10, 4f, NearAssistAllyRole.RangedDamage, Identity(70), 2),
            PressureCandidate(5, 2f, NearAssistAllyRole.MeleeDamage, Identity(80), 2),
        ];

        Equal(
            2,
            NearAssistPressureSelectionRules.SelectBestIndex(candidates, true, true),
            "role then distance then stable entity ID");
    }

    public static void NearAssistPressureInvalidCandidatesFailClosed()
    {
        NearAssistPressureSelectionCandidate[] candidates =
        [
            PressureCandidate(0, 1f, NearAssistAllyRole.RangedDamage, Identity(30), 2),
            PressureCandidate(10, float.NaN, NearAssistAllyRole.RangedDamage, Identity(40), 2),
            PressureCandidate(20, 2f, NearAssistAllyRole.RangedDamage, default, 2),
            PressureCandidate(30, 2f, NearAssistAllyRole.RangedDamage, Identity(50), -1),
        ];

        Equal(
            -1,
            NearAssistPressureSelectionRules.SelectBestIndex(candidates, true, true),
            "invalid enabled candidates");
    }

    private static TargetPressureActorIdentity Identity(uint entityId) =>
        new(10_000UL + entityId, entityId);

    private static TargetPressureEnemyObservation Enemy(
        TargetPressureActorIdentity actor,
        uint jobId = 30,
        int slot = 0,
        TargetPressureActorIdentity? hardTarget = null,
        TargetPressureActorIdentity? castTarget = null) =>
        new(
            actor,
            hardTarget,
            castTarget,
            jobId,
            slot,
            IsHostile: true,
            IsDead: false,
            IsTargetable: true);

    private static TargetPressureAllyObservation Ally(
        TargetPressureActorIdentity actor,
        TargetPressureActorIdentity hardTarget) =>
        new(
            actor,
            hardTarget,
            IsAlly: true,
            IsDead: false,
            IsTargetable: true);

    private static TargetPressurePartyAllyObservation PartyAlly(
        TargetPressureActorIdentity actor) =>
        new(
            actor,
            IsPartyMember: true,
            IsDead: false,
            IsTargetable: true);

    private static NearAssistPressureSelectionCandidate PressureCandidate(
        uint allyEntityId,
        float distance,
        NearAssistAllyRole role,
        TargetPressureActorIdentity enemyTarget,
        int allyTargetCount) =>
        new(
            new NearAssistAllySelectionCandidate(
                allyEntityId,
                distance * distance,
                role),
            enemyTarget,
            allyTargetCount);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{label}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
        }
    }

    private static void Throws<T>(Action action, string label)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(T).Name}: {label}");
    }
}
