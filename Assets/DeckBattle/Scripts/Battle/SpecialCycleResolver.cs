using System;

namespace DeckBattle
{
    public static class SpecialCycleResolver
    {
        private const double DeadlineEpsilon = 0.000001d;
        public static void AdvanceActiveCasts(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));

            workspace.Clear();
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || unit.SpecialPhase == UnitSpecialPhase.Idle)
                {
                    continue;
                }

                if (unit.SpecialPhase == UnitSpecialPhase.Casting
                    && (!unit.IsAlive || !UnitActionRules.CanActivateSpecial(unit)))
                {
                    CancelActiveSpecial(unit, eventQueue, simulation);
                    continue;
                }

                if (unit.SpecialPhase == UnitSpecialPhase.RecoveryLock)
                {
                    if (HasReachedDeadline(simulation.ElapsedTime, unit.ManaLockEndTime))
                    {
                        unit.LastSpecialRecoveryEndTime = simulation.ElapsedTime;
                        ResetToIdle(unit);
                    }

                    continue;
                }

                AdvanceCast(simulation, unit, eventQueue, workspace);
            }

            ResolveSlamImpacts(simulation, eventQueue, workspace);
            ResolveFuryStrikes(simulation, eventQueue, workspace);
            CompleteDueCasts(simulation, eventQueue);
        }

        public static void StartReadyCasts(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            float tickDuration)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (tickDuration <= 0f) throw new ArgumentOutOfRangeException(nameof(tickDuration));

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (!UnitActionRules.CanStartSpecialCast(simulation, unit))
                {
                    continue;
                }

                UnitSpecialCombatSpec special = unit.CombatSpec.Special;
                UnitRuntimeState target = null;
                if (special.Kind == UnitSpecialKind.Longshot)
                {
                    if (!UnitActionRules.TryGetLongshotTarget(simulation, unit, out target))
                    {
                        continue;
                    }
                }
                else if (UnitActionRules.SpecialRequiresTarget(special.Kind)
                    && !UnitActionRules.TryGetTargetedSpecialTarget(simulation, unit, out target))
                {
                    continue;
                }

                StartCast(simulation, unit, target, eventQueue, tickDuration);
            }
        }

        public static bool CancelActiveSpecial(
            UnitRuntimeState unit,
            BattleEventQueue eventQueue = null,
            BattleSimulation simulation = null)
        {
            if (unit == null || unit.SpecialPhase != UnitSpecialPhase.Casting)
            {
                return false;
            }

            eventQueue?.Enqueue(BattleEvent.SpecialCastCancelled(
                unit.UnitId,
                unit.CombatSpec.Special.Kind,
                unit.SpecialSequenceId));
            EnterRecoveryLock(
                unit,
                simulation != null ? simulation.ElapsedTime : 0d,
                simulation != null
                    ? simulation.Tuning.SpecialRecoveryLockDuration
                    : BattleRuntimeTuning.Default.SpecialRecoveryLockDuration);
            return true;
        }

        private static void StartCast(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            UnitRuntimeState target,
            BattleEventQueue eventQueue,
            float tickDuration)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            float castDuration = Math.Max(tickDuration, special.CastDuration);
            float effectDelay = Math.Min(castDuration, Math.Max(0f, special.EffectDelay));

            unit.SpecialPhase = UnitSpecialPhase.Casting;
            unit.SpecialSequenceId++;
            unit.SpecialCastStartTime = simulation.ElapsedTime;
            unit.SpecialCastEndTime = simulation.ElapsedTime + castDuration;
            unit.SpecialEffectTime = special.Kind == UnitSpecialKind.FurySwipes
                ? double.PositiveInfinity
                : simulation.ElapsedTime + effectDelay;
            unit.LockedSpecialTargetUnitId = target != null
                ? target.UnitId
                : UnitRuntimeState.NoTargetUnitId;
            unit.SpecialStrikesResolved = 0;
            unit.NextSpecialStrikeTime = special.Kind == UnitSpecialKind.FurySwipes
                ? simulation.ElapsedTime + castDuration / special.StrikeCount
                : double.PositiveInfinity;
            unit.ManaLockEndTime = double.PositiveInfinity;
            unit.CurrentMana = 0;
            unit.PassiveManaRemainder = 0L;

            eventQueue?.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));
            AttackCycleResolver.StartCooldownForSpecialCast(simulation, unit, eventQueue);
            eventQueue?.Enqueue(BattleEvent.SpecialCastStarted(
                unit.UnitId,
                unit.LockedSpecialTargetUnitId,
                special.Kind,
                unit.SpecialSequenceId,
                castDuration,
                target != null ? target.CurrentHex : default));
        }

        private static void AdvanceCast(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue,
            Workspace workspace)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            if (special.Kind == UnitSpecialKind.FurySwipes)
            {
                if (TryGetSpecialTarget(
                        simulation,
                        unit,
                        special,
                        CanRetargetBeforeFirstPayload(unit, special),
                        eventQueue,
                        out UnitRuntimeState target))
                {
                    CollectDueFuryStrikes(simulation, unit, target, workspace);
                }

                return;
            }

            if (!HasReachedDeadline(simulation.ElapsedTime, unit.SpecialEffectTime))
            {
                return;
            }

            bool canRetargetBeforePayload = CanRetargetBeforeFirstPayload(unit, special);
            unit.SpecialEffectTime = double.PositiveInfinity;
            switch (special.Kind)
            {
                case UnitSpecialKind.HasteBurst:
                    if (TryApplySpecial(simulation, unit, special, eventQueue))
                    {
                        AttackCycleResolver.RefreshCooldownForSpecialCast(simulation, unit);
                    }
                    break;
                case UnitSpecialKind.Slam:
                    workspace.AddSlamImpact(unit, unit.SpecialSequenceId, unit.CurrentHex);
                    break;
                case UnitSpecialKind.MegaArrow:
                    if (TryGetSpecialTarget(
                            simulation,
                            unit,
                            special,
                            canRetargetBeforePayload,
                            eventQueue,
                            out UnitRuntimeState megaArrowTarget))
                    {
                        LaunchMegaArrow(simulation, unit, megaArrowTarget, special, eventQueue);
                    }
                    break;
                case UnitSpecialKind.Longshot:
                    if (TryGetSpecialTarget(
                            simulation,
                            unit,
                            special,
                            canRetargetBeforePayload,
                            eventQueue,
                            out UnitRuntimeState longshotTarget))
                    {
                        LaunchLongshot(simulation, unit, longshotTarget, special, eventQueue);
                    }
                    break;
            }
        }

        private static void CompleteDueCasts(BattleSimulation simulation, BattleEventQueue eventQueue)
        {
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || unit.SpecialPhase != UnitSpecialPhase.Casting)
                {
                    continue;
                }

                UnitSpecialCombatSpec special = unit.CombatSpec.Special;
                if (special.Kind == UnitSpecialKind.FurySwipes
                    && unit.SpecialStrikesResolved < special.StrikeCount)
                {
                    continue;
                }

                if (HasReachedDeadline(simulation.ElapsedTime, unit.SpecialCastEndTime))
                {
                    CompleteCast(simulation, unit, eventQueue);
                }
            }
        }

        private static void CompleteCast(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            int sequenceId = unit.SpecialSequenceId;
            EnterRecoveryLock(unit, simulation.ElapsedTime, simulation.Tuning.SpecialRecoveryLockDuration);
            eventQueue?.Enqueue(BattleEvent.SpecialCastCompleted(
                unit.UnitId,
                special.Kind,
                sequenceId,
                special.CastDuration));
        }

        private static void LaunchMegaArrow(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            UnitSpecialCombatSpec special,
            BattleEventQueue eventQueue)
        {
            int damage = DamageCalculator.CalculateSpecialDamage(
                attacker,
                target,
                special.AttackDamageMultiplier,
                simulation.Tuning);
            var impact = new ProjectileImpactCombatSpec(
                DamageKind.Special,
                special.AppliedStatus,
                special.AppliedStatusLifetimeMode,
                special.AppliedStatusDuration);
            ProjectileRuntimeState projectile = simulation.SpawnProjectile(
                attacker,
                target,
                special.Projectile,
                damage,
                false,
                impact);
            eventQueue?.Enqueue(BattleEvent.SpecialStrikeFired(
                attacker.UnitId,
                target.UnitId,
                special.Kind,
                attacker.SpecialSequenceId,
                1,
                target.CurrentHex));
            eventQueue?.Enqueue(BattleEvent.ProjectileLaunched(
                projectile.ProjectileId,
                attacker.UnitId,
                target.UnitId,
                projectile.FromHex,
                projectile.LastKnownTargetHex,
                projectile.TravelDuration,
                special.Projectile.PresentationId));
        }

        private static void LaunchLongshot(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            UnitSpecialCombatSpec special,
            BattleEventQueue eventQueue)
        {
            int damage = DamageCalculator.CalculateSpecialDamage(
                attacker,
                target,
                special.AttackDamageMultiplier,
                simulation.Tuning);
            var impact = new ProjectileImpactCombatSpec(
                DamageKind.Special,
                default,
                StatusLifetimeMode.UseDefinitionDuration,
                0f,
                special.ExecuteHpThresholdPercent);
            ProjectileRuntimeState projectile = simulation.SpawnProjectile(
                attacker,
                target,
                special.Projectile,
                damage,
                false,
                impact);
            eventQueue?.Enqueue(BattleEvent.SpecialStrikeFired(
                attacker.UnitId,
                target.UnitId,
                special.Kind,
                attacker.SpecialSequenceId,
                1,
                target.CurrentHex));
            eventQueue?.Enqueue(BattleEvent.ProjectileLaunched(
                projectile.ProjectileId,
                attacker.UnitId,
                target.UnitId,
                projectile.FromHex,
                projectile.LastKnownTargetHex,
                projectile.TravelDuration,
                special.Projectile.PresentationId));
        }

        private static void CollectDueFuryStrikes(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            UnitRuntimeState target,
            Workspace workspace)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            while (unit.SpecialStrikesResolved < special.StrikeCount
                && HasReachedDeadline(simulation.ElapsedTime, unit.NextSpecialStrikeTime))
            {
                int strikeIndex = unit.SpecialStrikesResolved + 1;
                workspace.AddFuryStrike(unit, target, unit.SpecialSequenceId, strikeIndex);
                unit.SpecialStrikesResolved = strikeIndex;
                unit.NextSpecialStrikeTime = unit.SpecialCastStartTime
                    + special.CastDuration * (unit.SpecialStrikesResolved + 1) / special.StrikeCount;
            }
        }

        private static void ResolveSlamImpacts(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace)
        {
            for (int impactIndex = 0; impactIndex < workspace.SlamImpactCount; impactIndex++)
            {
                UnitRuntimeState attacker = workspace.SlamImpactAttackers[impactIndex];
                if (attacker == null || attacker.CombatSpec.Special.Kind != UnitSpecialKind.Slam)
                {
                    continue;
                }

                UnitSpecialCombatSpec special = attacker.CombatSpec.Special;
                HexCoord centerHex = workspace.SlamImpactCenters[impactIndex];
                eventQueue?.Enqueue(BattleEvent.SpecialAreaImpact(
                    attacker.UnitId,
                    special.Kind,
                    workspace.SlamImpactSequenceIds[impactIndex],
                    centerHex,
                    special.EffectRadius));

                for (int unitIndex = 0; unitIndex < simulation.Units.Count; unitIndex++)
                {
                    UnitRuntimeState target = simulation.Units[unitIndex];
                    if (target == null
                        || !target.IsAlive
                        || target.Side == attacker.Side
                        || simulation.Board.Distance(centerHex, target.CurrentHex) > special.EffectRadius)
                    {
                        continue;
                    }

                    int damage = DamageCalculator.CalculateSpecialDamage(
                        attacker,
                        target,
                        special.AttackDamageMultiplier,
                        simulation.Tuning);
                    DamageResolver.Resolve(
                        simulation,
                        target,
                        new DamageRequest(attacker, damage, DamageKind.Special, false),
                        eventQueue);
                }
            }
        }

        private static void ResolveFuryStrikes(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace)
        {
            for (int i = 0; i < workspace.FuryStrikeIntentCount; i++)
            {
                UnitRuntimeState attacker = workspace.FuryStrikeAttackers[i];
                UnitRuntimeState target = workspace.FuryStrikeTargets[i];
                if (attacker == null || target == null)
                {
                    continue;
                }

                UnitSpecialCombatSpec special = attacker.CombatSpec.Special;
                eventQueue?.Enqueue(BattleEvent.SpecialStrikeFired(
                    attacker.UnitId,
                    target.UnitId,
                    special.Kind,
                    workspace.FuryStrikeSequenceIds[i],
                    workspace.FuryStrikeIndices[i],
                    target.CurrentHex));
                int damage = DamageCalculator.CalculateSpecialDamage(
                    attacker,
                    target,
                    special.AttackDamageMultiplier,
                    simulation.Tuning);
                DamageResolver.Resolve(
                    simulation,
                    target,
                    new DamageRequest(attacker, damage, DamageKind.Special, false),
                    eventQueue);
            }
        }

        private static bool TryGetLockedTarget(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            out UnitRuntimeState target)
        {
            target = null;
            return unit != null
                && unit.LockedSpecialTargetUnitId != UnitRuntimeState.NoTargetUnitId
                && simulation.TryGetUnitById(unit.LockedSpecialTargetUnitId, out target)
                && target != null
                && target.Side != unit.Side;
        }

        private static bool TryGetSpecialTarget(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            UnitSpecialCombatSpec special,
            bool canRetargetBeforePayload,
            BattleEventQueue eventQueue,
            out UnitRuntimeState target)
        {
            if (!TryGetLockedTarget(simulation, unit, out target)
                || target.IsAlive
                || !canRetargetBeforePayload
                || !UnitActionRules.TryGetReplacementSpecialTarget(simulation, unit, special.Kind, out UnitRuntimeState replacement))
            {
                return target != null;
            }

            unit.LockedSpecialTargetUnitId = replacement.UnitId;
            unit.SetTarget(replacement);
            target = replacement;
            eventQueue?.Enqueue(BattleEvent.UnitTargetChanged(
                unit.UnitId,
                replacement.UnitId,
                replacement.CurrentHex));
            return true;
        }

        private static bool CanRetargetBeforeFirstPayload(UnitRuntimeState unit, UnitSpecialCombatSpec special)
        {
            if (special.Kind == UnitSpecialKind.FurySwipes)
            {
                return unit.SpecialStrikesResolved == 0;
            }

            return (special.Kind == UnitSpecialKind.MegaArrow || special.Kind == UnitSpecialKind.Longshot)
                && !double.IsPositiveInfinity(unit.SpecialEffectTime);
        }

        private static bool TryApplySpecial(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            UnitSpecialCombatSpec special,
            BattleEventQueue eventQueue)
        {
            if (!special.IsValid
                || special.Kind != UnitSpecialKind.HasteBurst
                || special.AppliedStatus.Kind != StatusKind.Haste)
            {
                return false;
            }

            StatusApplicationResult result = StatusResolver.TryApply(
                simulation,
                unit,
                new StatusApplicationRequest(
                    special.AppliedStatus,
                    unit.UnitId,
                    special.AppliedStatusDuration,
                    lifetimeMode: special.AppliedStatusLifetimeMode),
                eventQueue);
            return result == StatusApplicationResult.Applied
                || result == StatusApplicationResult.Refreshed;
        }

        private static void EnterRecoveryLock(UnitRuntimeState unit, double startTime, float duration)
        {
            unit.SpecialPhase = UnitSpecialPhase.RecoveryLock;
            unit.SpecialEffectTime = double.PositiveInfinity;
            unit.SpecialCastEndTime = double.PositiveInfinity;
            unit.SpecialCastStartTime = double.PositiveInfinity;
            unit.LockedSpecialTargetUnitId = UnitRuntimeState.NoTargetUnitId;
            unit.SpecialStrikesResolved = 0;
            unit.NextSpecialStrikeTime = double.PositiveInfinity;
            unit.ManaLockEndTime = startTime + duration;
        }

        private static bool HasReachedDeadline(double elapsedTime, double deadline)
        {
            return elapsedTime + DeadlineEpsilon >= deadline;
        }

        private static void ResetToIdle(UnitRuntimeState unit)
        {
            unit.SpecialPhase = UnitSpecialPhase.Idle;
            unit.SpecialEffectTime = double.PositiveInfinity;
            unit.SpecialCastEndTime = double.PositiveInfinity;
            unit.SpecialCastStartTime = double.PositiveInfinity;
            unit.LockedSpecialTargetUnitId = UnitRuntimeState.NoTargetUnitId;
            unit.SpecialStrikesResolved = 0;
            unit.NextSpecialStrikeTime = double.PositiveInfinity;
            unit.ManaLockEndTime = double.PositiveInfinity;
        }

        public sealed class Workspace
        {
            internal readonly UnitRuntimeState[] FuryStrikeAttackers;
            internal readonly UnitRuntimeState[] FuryStrikeTargets;
            internal readonly int[] FuryStrikeSequenceIds;
            internal readonly int[] FuryStrikeIndices;
            internal int FuryStrikeIntentCount;
            internal readonly UnitRuntimeState[] SlamImpactAttackers;
            internal readonly int[] SlamImpactSequenceIds;
            internal readonly HexCoord[] SlamImpactCenters;
            internal int SlamImpactCount;

            public Workspace(int unitCapacity)
            {
                int capacity = Math.Max(1, unitCapacity) * UnitSpecialDefinition.MaxStrikeCount;
                FuryStrikeAttackers = new UnitRuntimeState[capacity];
                FuryStrikeTargets = new UnitRuntimeState[capacity];
                FuryStrikeSequenceIds = new int[capacity];
                FuryStrikeIndices = new int[capacity];
                int slamCapacity = Math.Max(1, unitCapacity);
                SlamImpactAttackers = new UnitRuntimeState[slamCapacity];
                SlamImpactSequenceIds = new int[slamCapacity];
                SlamImpactCenters = new HexCoord[slamCapacity];
            }

            internal void AddFuryStrike(UnitRuntimeState attacker, UnitRuntimeState target, int sequenceId, int strikeIndex)
            {
                FuryStrikeAttackers[FuryStrikeIntentCount] = attacker;
                FuryStrikeTargets[FuryStrikeIntentCount] = target;
                FuryStrikeSequenceIds[FuryStrikeIntentCount] = sequenceId;
                FuryStrikeIndices[FuryStrikeIntentCount] = strikeIndex;
                FuryStrikeIntentCount++;
            }

            internal void AddSlamImpact(UnitRuntimeState attacker, int sequenceId, HexCoord centerHex)
            {
                SlamImpactAttackers[SlamImpactCount] = attacker;
                SlamImpactSequenceIds[SlamImpactCount] = sequenceId;
                SlamImpactCenters[SlamImpactCount] = centerHex;
                SlamImpactCount++;
            }

            internal void Clear()
            {
                FuryStrikeIntentCount = 0;
                SlamImpactCount = 0;
            }
        }
    }
}
