using System;

namespace DeckBattle
{
    public static class AttackCycleResolver
    {
        public static CombatResolutionResult Resolve(BattleSimulation simulation, BattleEventQueue eventQueue, Workspace workspace)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));

            workspace.Clear();
            int attacks = 0;
            int totalDamage = 0;
            int deaths = 0;

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || unit.AttackPhase != UnitAttackPhase.Windup) continue;

                if (!unit.IsAlive || !TryGetLockedLiveTarget(simulation, unit, out UnitRuntimeState lockedTarget))
                {
                    CancelWindup(unit, eventQueue);
                    workspace.Cancelled[i] = true;
                    continue;
                }

                if (simulation.ElapsedTime >= unit.WindupEndTime)
                {
                    workspace.FireUnits[workspace.FireCount] = unit;
                    workspace.FireTargets[workspace.FireCount] = lockedTarget;
                    workspace.FireCount++;
                }
            }

            // The collection pass above is the simultaneity boundary: an attacker
            // admitted here may fire even if an earlier intent defeats it.
            for (int i = 0; i < workspace.FireCount; i++)
            {
                UnitRuntimeState attacker = workspace.FireUnits[i];
                UnitRuntimeState target = workspace.FireTargets[i];

                int attackBonus = attacker.AttackBonusNextCombat;
                bool isCritical;
                int damage = DamageCalculator.CalculateDamage(attacker.Definition, target.Definition, attackBonus, simulation.Random, out isCritical);
                attacker.AttackBonusNextCombat = 0;
                eventQueue?.Enqueue(BattleEvent.AttackFired(attacker.UnitId, target.UnitId, attacker.AttackSequenceId));
                // Retained for consumers built before the phase-specific events.
                eventQueue?.Enqueue(BattleEvent.UnitAttackStarted(attacker.UnitId, target.UnitId));
                CombatResolver.AddMana(simulation, attacker, attacker.Definition.ManaPerAttack, eventQueue);

                ProjectileDefinition projectileDefinition = attacker.Definition.Projectile;
                bool useProjectile = attacker.Definition.UnitType == UnitType.Range && projectileDefinition != null;
                if (useProjectile)
                {
                    ProjectileRuntimeState projectile = simulation.SpawnProjectile(attacker, target, projectileDefinition, damage, isCritical);
                    eventQueue?.Enqueue(BattleEvent.ProjectileLaunched(projectile.ProjectileId, attacker.UnitId, target.UnitId, projectile.FromHex, projectile.LastKnownTargetHex, projectile.TravelDuration));
                }
                else
                {
                    HitResolutionResult hit = HitResolver.ResolveHit(simulation, attacker, target, damage, isCritical, eventQueue);
                    if (hit.DidHit)
                    {
                        totalDamage += hit.Damage;
                        if (hit.Died) deaths++;
                    }
                }

                attacker.NextAttackTime = attacker.AttackCycleStartTime + simulation.Tuning.GetAttackCooldown(attacker.Definition, attacker);
                attacker.AttackPhase = UnitAttackPhase.Winddown;
                attacker.LockedAttackTargetUnitId = UnitRuntimeState.NoTargetUnitId;
                attacker.WindupEndTime = double.PositiveInfinity;
                attacker.WinddownEndTime = simulation.ElapsedTime + attacker.Definition.AttackWinddownDuration;
                attacks++;
            }

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || unit.AttackPhase != UnitAttackPhase.Winddown || simulation.ElapsedTime < unit.WinddownEndTime) continue;
                unit.AttackPhase = UnitAttackPhase.AcquireReload;
                unit.WinddownEndTime = double.PositiveInfinity;
                eventQueue?.Enqueue(BattleEvent.AttackWinddownEnded(unit.UnitId, unit.AttackSequenceId));
            }

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || !unit.IsAlive || workspace.Cancelled[i] || unit.AttackPhase != UnitAttackPhase.AcquireReload || unit.IsMoving || simulation.ElapsedTime < unit.NextAttackTime) continue;
                if (!TryGetUnitById(simulation, unit.TargetUnitId, out UnitRuntimeState target) || !target.IsAlive) continue;
                if (simulation.Board.Distance(unit.CurrentHex, target.CurrentHex) > simulation.Tuning.GetAttackRange(unit.Definition)) continue;

                unit.AttackPhase = UnitAttackPhase.Windup;
                unit.LockedAttackTargetUnitId = target.UnitId;
                unit.AttackSequenceId++;
                unit.AttackCycleStartTime = unit.NextAttackTime;
                unit.WindupEndTime = simulation.ElapsedTime + unit.Definition.AttackWindupDuration;
                eventQueue?.Enqueue(BattleEvent.AttackWindupStarted(unit.UnitId, target.UnitId, unit.AttackSequenceId, unit.Definition.AttackWindupDuration));
            }

            // A zero-duration windup advances through its atomic Fire stage in
            // this tick. Collect first so every committed attacker gets its fire
            // even when another intent defeats it during resolution.
            workspace.FireCount = 0;
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState attacker = simulation.Units[i];
                if (attacker == null || attacker.AttackPhase != UnitAttackPhase.Windup || attacker.Definition.AttackWindupDuration > 0f) continue;
                if (!TryGetUnitById(simulation, attacker.LockedAttackTargetUnitId, out UnitRuntimeState target)) continue;
                workspace.FireUnits[workspace.FireCount++] = attacker;
                workspace.FireTargets[workspace.FireCount - 1] = target;
            }

            for (int i = 0; i < workspace.FireCount; i++)
            {
                UnitRuntimeState attacker = workspace.FireUnits[i];
                UnitRuntimeState target = workspace.FireTargets[i];

                int attackBonus = attacker.AttackBonusNextCombat;
                bool isCritical;
                int damage = DamageCalculator.CalculateDamage(attacker.Definition, target.Definition, attackBonus, simulation.Random, out isCritical);
                attacker.AttackBonusNextCombat = 0;
                eventQueue?.Enqueue(BattleEvent.AttackFired(attacker.UnitId, target.UnitId, attacker.AttackSequenceId));
                eventQueue?.Enqueue(BattleEvent.UnitAttackStarted(attacker.UnitId, target.UnitId));
                CombatResolver.AddMana(simulation, attacker, attacker.Definition.ManaPerAttack, eventQueue);

                ProjectileDefinition projectileDefinition = attacker.Definition.Projectile;
                if (attacker.Definition.UnitType == UnitType.Range && projectileDefinition != null)
                {
                    ProjectileRuntimeState projectile = simulation.SpawnProjectile(attacker, target, projectileDefinition, damage, isCritical);
                    eventQueue?.Enqueue(BattleEvent.ProjectileLaunched(projectile.ProjectileId, attacker.UnitId, target.UnitId, projectile.FromHex, projectile.LastKnownTargetHex, projectile.TravelDuration));
                }
                else
                {
                    HitResolutionResult hit = HitResolver.ResolveHit(simulation, attacker, target, damage, isCritical, eventQueue);
                    if (hit.DidHit) { totalDamage += hit.Damage; if (hit.Died) deaths++; }
                }

                attacker.NextAttackTime = attacker.AttackCycleStartTime + simulation.Tuning.GetAttackCooldown(attacker.Definition, attacker);
                attacker.AttackPhase = UnitAttackPhase.Winddown;
                attacker.LockedAttackTargetUnitId = UnitRuntimeState.NoTargetUnitId;
                attacker.WindupEndTime = double.PositiveInfinity;
                attacker.WinddownEndTime = simulation.ElapsedTime + attacker.Definition.AttackWinddownDuration;
                attacks++;
            }

            return new CombatResolutionResult(attacks, totalDamage, deaths);
        }

        private static void CancelWindup(UnitRuntimeState unit, BattleEventQueue eventQueue)
        {
            eventQueue?.Enqueue(BattleEvent.AttackWindupCancelled(unit.UnitId, unit.LockedAttackTargetUnitId, unit.AttackSequenceId));
            unit.AttackPhase = UnitAttackPhase.AcquireReload;
            unit.LockedAttackTargetUnitId = UnitRuntimeState.NoTargetUnitId;
            unit.WindupEndTime = double.PositiveInfinity;
        }

        private static bool TryGetLockedLiveTarget(BattleSimulation simulation, UnitRuntimeState attacker, out UnitRuntimeState target)
        {
            return TryGetUnitById(simulation, attacker.LockedAttackTargetUnitId, out target) && target.IsAlive;
        }

        private static bool TryGetUnitById(BattleSimulation simulation, int unitId, out UnitRuntimeState unit)
        {
            unit = null;
            return unitId != UnitRuntimeState.NoTargetUnitId && simulation.TryGetUnitById(unitId, out unit) && unit != null;
        }

        public sealed class Workspace
        {
            internal UnitRuntimeState[] FireUnits;
            internal UnitRuntimeState[] FireTargets;
            internal bool[] Cancelled;
            internal int FireCount;

            public Workspace(int unitCapacity)
            {
                int capacity = Math.Max(1, unitCapacity);
                FireUnits = new UnitRuntimeState[capacity];
                FireTargets = new UnitRuntimeState[capacity];
                Cancelled = new bool[capacity];
            }

            internal void Clear()
            {
                FireCount = 0;
                Array.Clear(Cancelled, 0, Cancelled.Length);
            }
        }
    }
}
