using System;

namespace DeckBattle
{
    public static class SpecialCycleResolver
    {
        private const float RecoveryLockDuration = 0.5f;

        public static void Resolve(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace,
            float tickDuration)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (tickDuration <= 0f) throw new ArgumentOutOfRangeException(nameof(tickDuration));

            workspace.Clear();
            AdvanceActiveCycles(simulation, eventQueue, tickDuration);
            StartReadyWindups(simulation, eventQueue, workspace, tickDuration);
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
                // recovery lock and starts the ordinary attack cooldown here.
                EnterRecoveryLock(unit, simulation != null ? simulation.ElapsedTime : 0d);
                if (simulation != null)
                {
                    AttackCycleResolver.RestartCooldownAfterSpecial(simulation, unit, eventQueue);
                }
            }

            return true;
        }

        public static void AdvanceActiveCycles(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            float tickDuration)
        {
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
                        if (simulation.ElapsedTime >= unit.SpecialWindupEndTime)
                        {
                            BeginCast(simulation, unit, eventQueue, tickDuration);
                        }
                        break;
                    case UnitSpecialPhase.Casting:
                        if (simulation.ElapsedTime >= unit.SpecialCastEndTime)
                        {
                            CompleteCast(simulation, unit, eventQueue);
                        }
                        break;
                    case UnitSpecialPhase.RecoveryLock:
                        if (simulation.ElapsedTime >= unit.ManaLockEndTime)
                        {
                            ResetToIdle(unit);
                        }
                        break;
                }
            }
        }

        private static void BeginCast(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue,
            float tickDuration)
        {
            UnitSpecialCombatSpec special = unit.CombatSpec.Special;
            unit.SpecialPhase = UnitSpecialPhase.Casting;
            unit.SpecialWindupEndTime = double.PositiveInfinity;
            unit.CurrentMana = 0;
            eventQueue?.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));

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
            EnterRecoveryLock(unit, simulation.ElapsedTime);

            if (TryApplySpecial(simulation, unit, special, eventQueue))
            {
                eventQueue?.Enqueue(BattleEvent.UnitSpecialActivated(
                    unit.UnitId,
                    special.Kind,
                    special.AppliedStatus.DefaultDuration,
                    sequenceId));
            }

            // The special's own status (for example haste) affects the next
            // attack cycle because that cycle begins only after the cast ends.
            AttackCycleResolver.RestartCooldownAfterSpecial(simulation, unit, eventQueue);
        }

        private static void EnterRecoveryLock(UnitRuntimeState unit, double startTime)
        {
            unit.SpecialPhase = UnitSpecialPhase.RecoveryLock;
            unit.SpecialWindupEndTime = double.PositiveInfinity;
            unit.SpecialCastEndTime = double.PositiveInfinity;
            unit.ManaLockEndTime = startTime + RecoveryLockDuration;
        }

        private static void ResetToIdle(UnitRuntimeState unit)
        {
            unit.SpecialPhase = UnitSpecialPhase.Idle;
            unit.SpecialWindupEndTime = double.PositiveInfinity;
            unit.SpecialCastEndTime = double.PositiveInfinity;
            unit.ManaLockEndTime = double.PositiveInfinity;
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
                if (!UnitActionRules.CanStartSpecialWindup(unit))
                {
                    continue;
                }

                UnitSpecialCombatSpec special = unit.CombatSpec.Special;
                float duration = Math.Max(tickDuration, Math.Max(0f, special.WindupDuration));
                unit.SpecialPhase = UnitSpecialPhase.Windup;
                unit.SpecialSequenceId++;
                unit.SpecialWindupEndTime = simulation.ElapsedTime + duration;
                unit.SpecialCastEndTime = double.PositiveInfinity;
                unit.ManaLockEndTime = double.PositiveInfinity;
                eventQueue?.Enqueue(BattleEvent.SpecialWindupStarted(
                    unit.UnitId,
                    special.Kind,
                    unit.SpecialSequenceId,
                    duration));
            }
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
                new StatusApplicationRequest(special.AppliedStatus, unit.UnitId),
                eventQueue);
            return result == StatusApplicationResult.Applied
                || result == StatusApplicationResult.Refreshed;
        }

        public sealed class Workspace
        {
            public Workspace(int unitCapacity)
            {
            }

            internal void Clear()
            {
            }
        }
    }
}
