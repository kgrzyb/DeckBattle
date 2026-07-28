using System;

namespace DeckBattle
{
    public enum AttackResetResult
    {
        Applied = 0,
        IgnoredDuringWindup = 1,
        IgnoredOutsideWinddown = 2,
        IgnoredDead = 3
    }

    public static class AttackCycleResolver
    {
        public static CombatResolutionResult Resolve(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace,
            float tickDuration)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (tickDuration <= 0f) throw new ArgumentOutOfRangeException(nameof(tickDuration));

            workspace.Clear();
            CollectCompletedWindups(simulation, eventQueue, workspace);
            CombatResolutionResult combat = ResolveCommittedFires(simulation, eventQueue, workspace);
            CompleteWinddowns(simulation, eventQueue);
            StartReadyWindups(simulation, eventQueue, workspace, tickDuration);
            return combat;
        }

        public static AttackResetResult TryResetWinddown(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue = null)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (unit == null) throw new ArgumentNullException(nameof(unit));

            if (!unit.IsAlive)
            {
                return AttackResetResult.IgnoredDead;
            }

            if (unit.AttackPhase == UnitAttackPhase.Windup)
            {
                return AttackResetResult.IgnoredDuringWindup;
            }

            if (unit.AttackPhase != UnitAttackPhase.Winddown)
            {
                return AttackResetResult.IgnoredOutsideWinddown;
            }

            unit.NextAttackTime = simulation.ElapsedTime;
            CompleteWinddown(unit, eventQueue);
            return AttackResetResult.Applied;
        }

        public static bool CancelWindup(UnitRuntimeState unit, BattleEventQueue eventQueue = null)
        {
            if (unit == null || unit.AttackPhase != UnitAttackPhase.Windup)
            {
                return false;
            }

            CancelWindupInternal(unit, eventQueue);
            return true;
        }

        private static void CollectCompletedWindups(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace)
        {
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || unit.AttackPhase != UnitAttackPhase.Windup)
                {
                    continue;
                }

                if (!unit.IsAlive
                    || unit.IsMoving
                    || !TryGetLockedLiveTarget(simulation, unit, out UnitRuntimeState lockedTarget))
                {
                    CancelWindupInternal(unit, eventQueue);
                    workspace.Cancelled[i] = true;
                    continue;
                }

                if (simulation.ElapsedTime < unit.WindupEndTime)
                {
                    continue;
                }

                workspace.AddFire(unit, lockedTarget);
            }
        }

        private static CombatResolutionResult ResolveCommittedFires(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace)
        {
            int attacks = 0;
            int totalDamage = 0;
            int deaths = 0;

            // Collection is the simultaneity boundary. Every admitted attacker
            // fires even if an earlier committed attack defeats it.
            for (int i = 0; i < workspace.FireCount; i++)
            {
                UnitRuntimeState attacker = workspace.FireUnits[i];
                UnitRuntimeState target = workspace.FireTargets[i];
                int sequenceId = workspace.FireSequenceIds[i];

                attacker.AttackPhase = UnitAttackPhase.Winddown;
                attacker.LockedAttackTargetUnitId = UnitRuntimeState.NoTargetUnitId;
                attacker.WindupEndTime = double.PositiveInfinity;
                attacker.MarkTargetEngaged(target.UnitId);

                float remainingWinddown = (float)Math.Max(0d, attacker.NextAttackTime - simulation.ElapsedTime);
                eventQueue?.Enqueue(BattleEvent.AttackFired(attacker.UnitId, target.UnitId, sequenceId, remainingWinddown));
                // Retained for consumers built before the phase-specific events.
                eventQueue?.Enqueue(BattleEvent.UnitAttackStarted(attacker.UnitId, target.UnitId));

                int attackBonus = attacker.AttackBonusNextCombat;
                int damage = DamageCalculator.CalculateDamage(
                    attacker,
                    target,
                    attackBonus,
                    simulation.Tuning,
                    simulation.Random,
                    out bool isCritical);
                attacker.AttackBonusNextCombat = 0;

                CombatResolver.AddMana(simulation, attacker, attacker.Definition.ManaPerAttack, eventQueue);

                ProjectileDefinition projectileDefinition = attacker.Definition.Projectile;
                bool useProjectile = attacker.Definition.UnitType == UnitType.Range && projectileDefinition != null;
                if (useProjectile)
                {
                    ProjectileRuntimeState projectile = simulation.SpawnProjectile(
                        attacker,
                        target,
                        projectileDefinition,
                        damage,
                        isCritical);
                    eventQueue?.Enqueue(BattleEvent.ProjectileLaunched(
                        projectile.ProjectileId,
                        attacker.UnitId,
                        target.UnitId,
                        projectile.FromHex,
                        projectile.LastKnownTargetHex,
                        projectile.TravelDuration));
                }
                else
                {
                    HitResolutionResult hit = HitResolver.ResolveHit(
                        simulation,
                        attacker,
                        target,
                        damage,
                        isCritical,
                        eventQueue);
                    if (hit.DidHit)
                    {
                        totalDamage += hit.Damage;
                        if (hit.Died)
                        {
                            deaths++;
                        }
                    }
                }

                attacks++;
            }

            return new CombatResolutionResult(attacks, totalDamage, deaths);
        }

        private static void CompleteWinddowns(BattleSimulation simulation, BattleEventQueue eventQueue)
        {
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null
                    || unit.AttackPhase != UnitAttackPhase.Winddown
                    || simulation.ElapsedTime < unit.NextAttackTime)
                {
                    continue;
                }

                CompleteWinddown(unit, eventQueue);
            }
        }

        private static void StartReadyWindups(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace,
            float tickDuration)
        {
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null
                    || !UnitActionRules.CanStartAttackWindup(unit)
                    || workspace.Cancelled[i]
                    || unit.AttackPhase != UnitAttackPhase.AcquireReload
                    || unit.IsMoving
                    || simulation.ElapsedTime < unit.NextAttackTime)
                {
                    continue;
                }

                if (!TryGetUnitById(simulation, unit.TargetUnitId, out UnitRuntimeState target)
                    || !target.IsAlive
                    || simulation.Board.Distance(unit.CurrentHex, target.CurrentHex) > simulation.Tuning.GetAttackRange(unit.Definition))
                {
                    continue;
                }

                float attackCycleDuration = simulation.Tuning.GetAttackCooldown(unit.Definition, unit);
                float windupPercent = Math.Max(0f, Math.Min(1f, unit.Definition.AttackWindupPercent));
                float windupDuration = Math.Max(tickDuration, attackCycleDuration * windupPercent);
                attackCycleDuration = Math.Max(attackCycleDuration, windupDuration);

                unit.AttackPhase = UnitAttackPhase.Windup;
                unit.LockedAttackTargetUnitId = target.UnitId;
                unit.AttackSequenceId++;
                unit.AttackCycleStartTime = simulation.ElapsedTime;
                unit.WindupEndTime = unit.AttackCycleStartTime + windupDuration;
                unit.NextAttackTime = unit.AttackCycleStartTime + attackCycleDuration;
                eventQueue?.Enqueue(BattleEvent.AttackWindupStarted(
                    unit.UnitId,
                    target.UnitId,
                    unit.AttackSequenceId,
                    windupDuration));
            }
        }

        private static void CompleteWinddown(UnitRuntimeState unit, BattleEventQueue eventQueue)
        {
            unit.AttackPhase = UnitAttackPhase.AcquireReload;
            unit.AttackCycleStartTime = double.PositiveInfinity;
            eventQueue?.Enqueue(BattleEvent.AttackWinddownEnded(unit.UnitId, unit.AttackSequenceId));
        }

        private static void CancelWindupInternal(UnitRuntimeState unit, BattleEventQueue eventQueue)
        {
            eventQueue?.Enqueue(BattleEvent.AttackWindupCancelled(
                unit.UnitId,
                unit.LockedAttackTargetUnitId,
                unit.AttackSequenceId));
            unit.AttackPhase = UnitAttackPhase.AcquireReload;
            unit.LockedAttackTargetUnitId = UnitRuntimeState.NoTargetUnitId;
            unit.AttackCycleStartTime = double.PositiveInfinity;
            unit.WindupEndTime = double.PositiveInfinity;
        }

        private static bool TryGetLockedLiveTarget(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            out UnitRuntimeState target)
        {
            return TryGetUnitById(simulation, attacker.LockedAttackTargetUnitId, out target)
                && target.IsAlive;
        }

        private static bool TryGetUnitById(
            BattleSimulation simulation,
            int unitId,
            out UnitRuntimeState unit)
        {
            unit = null;
            return unitId != UnitRuntimeState.NoTargetUnitId
                && simulation.TryGetUnitById(unitId, out unit)
                && unit != null;
        }

        public sealed class Workspace
        {
            internal readonly UnitRuntimeState[] FireUnits;
            internal readonly UnitRuntimeState[] FireTargets;
            internal readonly int[] FireSequenceIds;
            internal readonly bool[] Cancelled;
            internal int FireCount;

            public Workspace(int unitCapacity)
            {
                int capacity = Math.Max(1, unitCapacity);
                FireUnits = new UnitRuntimeState[capacity];
                FireTargets = new UnitRuntimeState[capacity];
                FireSequenceIds = new int[capacity];
                Cancelled = new bool[capacity];
            }

            internal void AddFire(UnitRuntimeState attacker, UnitRuntimeState target)
            {
                FireUnits[FireCount] = attacker;
                FireTargets[FireCount] = target;
                FireSequenceIds[FireCount] = attacker.AttackSequenceId;
                FireCount++;
            }

            internal void Clear()
            {
                FireCount = 0;
                Array.Clear(Cancelled, 0, Cancelled.Length);
            }
        }
    }
}
