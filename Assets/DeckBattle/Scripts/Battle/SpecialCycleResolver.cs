using System;

namespace DeckBattle
{
    public static class SpecialCycleResolver
    {
        public static void Resolve(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace,
            float tickDuration)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (tickDuration <= 0f) throw new ArgumentOutOfRangeException(nameof(tickDuration));

            AdvanceActiveCycles(simulation, eventQueue, workspace, tickDuration);
            StartReadyWindups(simulation, eventQueue, tickDuration);
        }

        public static bool CancelWindup(UnitRuntimeState unit, BattleEventQueue eventQueue = null, BattleSimulation simulation = null)
        {
            if (unit == null
                || (unit.SpecialPhase != UnitSpecialPhase.Windup
                    && unit.SpecialPhase != UnitSpecialPhase.Casting))
            {
                return false;
            }

            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            eventQueue?.Enqueue(BattleEvent.SpecialWindupCancelled(
                unit.UnitId,
                special.Kind,
                unit.SpecialSequenceId));
            if (unit.SpecialPhase == UnitSpecialPhase.Windup)
            {
                ResetToIdle(unit);
            }
            else
            {
                // Mana is committed when casting begins. A fizzle keeps the
                // recovery lock while the ordinary attack cooldown continues.
                EnterRecoveryLock(
                    unit,
                    simulation != null ? simulation.ElapsedTime : 0d,
                    simulation != null
                        ? simulation.Tuning.SpecialRecoveryLockDuration
                        : BattleRuntimeTuning.Default.SpecialRecoveryLockDuration);
            }

            return true;
        }

        public static void AdvanceActiveCycles(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace,
            float tickDuration)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (tickDuration <= 0f) throw new ArgumentOutOfRangeException(nameof(tickDuration));

            workspace.Clear();
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || unit.SpecialPhase == UnitSpecialPhase.Idle)
                {
                    continue;
                }

                if ((unit.SpecialPhase == UnitSpecialPhase.Windup || unit.SpecialPhase == UnitSpecialPhase.Casting)
                    && (!unit.IsAlive || !UnitActionRules.CanActivateSpecial(unit)))
                {
                    CancelWindup(unit, eventQueue, simulation);
                    continue;
                }

                switch (unit.SpecialPhase)
                {
                    case UnitSpecialPhase.Windup:
                        if (UnitActionRules.SpecialLocksTarget(unit.CombatSpec.Special.Kind)
                            && !TryGetLockedTarget(simulation, unit, out _))
                        {
                            CancelWindup(unit, eventQueue, simulation);
                            break;
                        }

                        if (simulation.ElapsedTime >= unit.SpecialWindupEndTime)
                        {
                            BeginCast(simulation, unit, eventQueue, workspace, tickDuration);
                        }
                        break;
                    case UnitSpecialPhase.Casting:
                        if (unit.CombatSpec.Special.Kind == UnitSpecialKind.FurySwipes)
                        {
                            if (!TryGetLockedTarget(simulation, unit, out UnitRuntimeState target))
                            {
                                CancelWindup(unit, eventQueue, simulation);
                                break;
                            }

                            CollectDueFuryStrikes(simulation, unit, target, workspace);
                        }
                        else if (simulation.ElapsedTime >= unit.SpecialCastEndTime)
                        {
                            CompleteCast(simulation, unit, eventQueue);
                        }
                        break;
                    case UnitSpecialPhase.RecoveryLock:
                        if (simulation.ElapsedTime >= unit.ManaLockEndTime)
                        {
                            unit.LastSpecialRecoveryEndTime = simulation.ElapsedTime;
                            ResetToIdle(unit);
                        }
                        break;
                    }
            }

            ResolveSlamImpacts(simulation, eventQueue, workspace);
            ResolveFuryStrikes(simulation, eventQueue, workspace);
            CompleteResolvedFuryCasts(simulation, eventQueue);
        }

        private static void BeginCast(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue,
            Workspace workspace,
            float tickDuration)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            UnitRuntimeState target = null;
            if (UnitActionRules.SpecialLocksTarget(special.Kind)
                && !TryGetLockedTarget(simulation, unit, out target))
            {
                CancelWindup(unit, eventQueue, simulation);
                return;
            }

            double windupEndTime = unit.SpecialWindupEndTime;
            unit.SpecialPhase = UnitSpecialPhase.Casting;
            unit.SpecialWindupEndTime = double.PositiveInfinity;
            unit.CurrentMana = 0;
            unit.SpecialCastStartTime = simulation.ElapsedTime;
            eventQueue?.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));
            AttackCycleResolver.StartCooldownForSpecialCast(simulation, unit, eventQueue);

            if (special.Kind == UnitSpecialKind.FurySwipes)
            {
                float interval = special.CastDuration / special.StrikeCount;
                unit.SpecialStrikesResolved = 0;
                unit.NextSpecialStrikeTime = simulation.ElapsedTime + interval;
                unit.SpecialCastEndTime = simulation.ElapsedTime + special.CastDuration;
                eventQueue?.Enqueue(BattleEvent.SpecialCastStarted(
                    unit.UnitId,
                    target.UnitId,
                    special.Kind,
                    unit.SpecialSequenceId,
                    special.CastDuration,
                    target.CurrentHex));
                return;
            }

            if (special.Kind == UnitSpecialKind.MegaArrow)
            {
                float resolvedWindupDuration = Math.Max(tickDuration, special.WindupDuration);
                unit.SpecialCastEndTime = windupEndTime - resolvedWindupDuration + special.CastDuration;
                eventQueue?.Enqueue(BattleEvent.SpecialCastStarted(
                    unit.UnitId,
                    target.UnitId,
                    special.Kind,
                    unit.SpecialSequenceId,
                    special.CastDuration,
                    target.CurrentHex));
                LaunchMegaArrow(simulation, unit, target, special, eventQueue);
                return;
            }

            if (special.Kind == UnitSpecialKind.Longshot)
            {
                float resolvedWindupDuration = Math.Max(tickDuration, special.WindupDuration);
                unit.SpecialCastEndTime = windupEndTime - resolvedWindupDuration + special.CastDuration;
                eventQueue?.Enqueue(BattleEvent.SpecialCastStarted(
                    unit.UnitId,
                    target.UnitId,
                    special.Kind,
                    unit.SpecialSequenceId,
                    special.CastDuration,
                    target.CurrentHex));
                LaunchLongshot(simulation, unit, target, special, eventQueue);
                return;
            }

            if (special.Kind == UnitSpecialKind.Slam)
            {
                unit.SpecialCastEndTime = simulation.ElapsedTime + Math.Max(tickDuration, special.CastDuration);
                workspace.AddSlamImpact(unit, unit.SpecialSequenceId, unit.CurrentHex);
                return;
            }

            if (special.CastDuration <= 0f)
            {
                CompleteCast(simulation, unit, eventQueue);
                return;
            }

            unit.SpecialCastEndTime = simulation.ElapsedTime + Math.Max(tickDuration, special.CastDuration);
        }

        private static void CompleteCast(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            int sequenceId = unit.SpecialSequenceId;

            if (special.Kind == UnitSpecialKind.Slam
                || special.Kind == UnitSpecialKind.MegaArrow
                || special.Kind == UnitSpecialKind.Longshot)
            {
                EnterRecoveryLock(unit, simulation.ElapsedTime, simulation.Tuning.SpecialRecoveryLockDuration);
                eventQueue?.Enqueue(BattleEvent.UnitSpecialActivated(
                    unit.UnitId,
                    special.Kind,
                    special.CastDuration,
                    sequenceId));
                return;
            }

            if (TryApplySpecial(simulation, unit, special, eventQueue))
            {
                eventQueue?.Enqueue(BattleEvent.UnitSpecialActivated(
                    unit.UnitId,
                    special.Kind,
                    special.AppliedStatus.DefaultDuration,
                    sequenceId));
                AttackCycleResolver.RefreshCooldownForSpecialCast(simulation, unit);
            }

            EnterRecoveryLock(unit, simulation.ElapsedTime, simulation.Tuning.SpecialRecoveryLockDuration);
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
            int sequenceId = attacker.SpecialSequenceId;
            eventQueue?.Enqueue(BattleEvent.SpecialStrikeFired(
                attacker.UnitId,
                target.UnitId,
                special.Kind,
                sequenceId,
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
            int sequenceId = attacker.SpecialSequenceId;
            eventQueue?.Enqueue(BattleEvent.SpecialStrikeFired(
                attacker.UnitId,
                target.UnitId,
                special.Kind,
                sequenceId,
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

        private static void EnterRecoveryLock(UnitRuntimeState unit, double startTime, float duration)
        {
            unit.SpecialPhase = UnitSpecialPhase.RecoveryLock;
            unit.SpecialWindupEndTime = double.PositiveInfinity;
            unit.SpecialCastEndTime = double.PositiveInfinity;
            unit.SpecialCastStartTime = double.PositiveInfinity;
            unit.LockedSpecialTargetUnitId = UnitRuntimeState.NoTargetUnitId;
            unit.SpecialStrikesResolved = 0;
            unit.NextSpecialStrikeTime = double.PositiveInfinity;
            unit.ManaLockEndTime = startTime + duration;
        }

        private static void ResetToIdle(UnitRuntimeState unit)
        {
            unit.SpecialPhase = UnitSpecialPhase.Idle;
            unit.SpecialWindupEndTime = double.PositiveInfinity;
            unit.SpecialCastEndTime = double.PositiveInfinity;
            unit.SpecialCastStartTime = double.PositiveInfinity;
            unit.LockedSpecialTargetUnitId = UnitRuntimeState.NoTargetUnitId;
            unit.SpecialStrikesResolved = 0;
            unit.NextSpecialStrikeTime = double.PositiveInfinity;
            unit.ManaLockEndTime = double.PositiveInfinity;
        }

        private static void StartReadyWindups(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            float tickDuration)
        {
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (!UnitActionRules.CanStartSpecialWindup(simulation, unit))
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

                float duration = Math.Max(tickDuration, Math.Max(0f, special.WindupDuration));
                unit.SpecialPhase = UnitSpecialPhase.Windup;
                unit.SpecialSequenceId++;
                unit.SpecialWindupEndTime = simulation.ElapsedTime + duration;
                unit.SpecialCastEndTime = double.PositiveInfinity;
                unit.SpecialCastStartTime = double.PositiveInfinity;
                unit.LockedSpecialTargetUnitId = target != null
                    ? target.UnitId
                    : UnitRuntimeState.NoTargetUnitId;
                unit.SpecialStrikesResolved = 0;
                unit.NextSpecialStrikeTime = double.PositiveInfinity;
                unit.ManaLockEndTime = double.PositiveInfinity;
                eventQueue?.Enqueue(BattleEvent.SpecialWindupStarted(
                    unit.UnitId,
                    special.Kind,
                    unit.SpecialSequenceId,
                    duration,
                    unit.LockedSpecialTargetUnitId,
                    target != null ? target.CurrentHex : default));
            }
        }

        private static void CollectDueFuryStrikes(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            UnitRuntimeState target,
            Workspace workspace)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            double castStartTime = unit.SpecialCastEndTime - special.CastDuration;
            while (unit.SpecialStrikesResolved < special.StrikeCount
                && simulation.ElapsedTime >= unit.NextSpecialStrikeTime)
            {
                int strikeIndex = unit.SpecialStrikesResolved + 1;
                workspace.AddFuryStrike(unit, target, unit.SpecialSequenceId, strikeIndex);
                unit.SpecialStrikesResolved = strikeIndex;
                unit.NextSpecialStrikeTime = castStartTime
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
                if (attacker == null)
                {
                    continue;
                }

                UnitSpecialCombatSpec special = attacker.CombatSpec.Special;
                if (special.Kind != UnitSpecialKind.Slam)
                {
                    continue;
                }

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

        private static void CompleteResolvedFuryCasts(
            BattleSimulation simulation,
            BattleEventQueue eventQueue)
        {
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null
                    || unit.SpecialPhase != UnitSpecialPhase.Casting
                    || unit.CombatSpec.Special.Kind != UnitSpecialKind.FurySwipes)
                {
                    continue;
                }

                UnitSpecialCombatSpec special = unit.CombatSpec.Special;
                if (unit.SpecialStrikesResolved >= special.StrikeCount)
                {
                    CompleteFuryCast(simulation, unit, eventQueue);
                }
            }
        }

        private static void CompleteFuryCast(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            int sequenceId = unit.SpecialSequenceId;
            EnterRecoveryLock(unit, simulation.ElapsedTime, simulation.Tuning.SpecialRecoveryLockDuration);
            eventQueue?.Enqueue(BattleEvent.UnitSpecialActivated(
                unit.UnitId,
                special.Kind,
                special.CastDuration,
                sequenceId));
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

            internal void AddFuryStrike(
                UnitRuntimeState attacker,
                UnitRuntimeState target,
                int sequenceId,
                int strikeIndex)
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
