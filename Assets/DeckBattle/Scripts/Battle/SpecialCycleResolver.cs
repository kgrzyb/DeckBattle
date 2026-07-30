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

            workspace.Clear();
            CollectCompletedWindups(simulation, eventQueue, workspace);
            ResolveCompletedWindups(simulation, eventQueue, workspace);
            StartReadyWindups(simulation, eventQueue, workspace, tickDuration);
        }

        public static bool CancelWindup(UnitRuntimeState unit, BattleEventQueue eventQueue = null)
        {
            if (unit == null || unit.SpecialPhase != UnitSpecialPhase.Windup)
            {
                return false;
            }

            UnitSpecialDefinition special = unit.Definition != null ? unit.Definition.Special : null;
            eventQueue?.Enqueue(BattleEvent.SpecialWindupCancelled(
                unit.UnitId,
                special != null ? special.Kind : UnitSpecialKind.None,
                unit.SpecialSequenceId));
            unit.SpecialPhase = UnitSpecialPhase.Idle;
            unit.SpecialWindupEndTime = double.PositiveInfinity;
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
                if (unit == null || unit.SpecialPhase != UnitSpecialPhase.Windup)
                {
                    continue;
                }

                if (!unit.IsAlive || !UnitActionRules.CanActivateSpecial(unit))
                {
                    CancelWindup(unit, eventQueue);
                    workspace.Cancelled[i] = true;
                    continue;
                }

                if (simulation.ElapsedTime >= unit.SpecialWindupEndTime)
                {
                    workspace.AddCompleted(unit, i);
                }
            }
        }

        private static void ResolveCompletedWindups(
            BattleSimulation simulation,
            BattleEventQueue eventQueue,
            Workspace workspace)
        {
            for (int i = 0; i < workspace.CompletedCount; i++)
            {
                UnitRuntimeState unit = workspace.CompletedUnits[i];
                UnitSpecialDefinition special = unit.Definition.Special;
                int sequenceId = unit.SpecialSequenceId;
                unit.SpecialPhase = UnitSpecialPhase.Idle;
                unit.SpecialWindupEndTime = double.PositiveInfinity;
                workspace.Resolved[workspace.CompletedIndices[i]] = true;

                if (!TryApplySpecial(simulation, unit, special, eventQueue))
                {
                    continue;
                }

                unit.CurrentMana = 0;
                eventQueue?.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));
                eventQueue?.Enqueue(BattleEvent.UnitSpecialActivated(
                    unit.UnitId,
                    special.Kind,
                    special.AppliedStatus.DefaultDuration,
                    sequenceId));
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
                if (workspace.Cancelled[i] || workspace.Resolved[i])
                {
                    continue;
                }

                UnitRuntimeState unit = simulation.Units[i];
                if (!UnitActionRules.CanStartSpecialWindup(unit))
                {
                    continue;
                }

                UnitSpecialDefinition special = unit.Definition.Special;
                float duration = Math.Max(tickDuration, Math.Max(0f, special.WindupDuration));
                unit.SpecialPhase = UnitSpecialPhase.Windup;
                unit.SpecialSequenceId++;
                unit.SpecialWindupEndTime = simulation.ElapsedTime + duration;
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
            UnitSpecialDefinition special,
            BattleEventQueue eventQueue)
        {
            if (special == null
                || special.Kind != UnitSpecialKind.HasteBurst
                || special.AppliedStatus == null
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
            internal UnitRuntimeState[] CompletedUnits;
            internal int[] CompletedIndices;
            internal bool[] Cancelled;
            internal bool[] Resolved;
            internal int CompletedCount;

            public Workspace(int unitCapacity)
            {
                int capacity = Math.Max(1, unitCapacity);
                CompletedUnits = new UnitRuntimeState[capacity];
                CompletedIndices = new int[capacity];
                Cancelled = new bool[capacity];
                Resolved = new bool[capacity];
            }

            internal void AddCompleted(UnitRuntimeState unit, int unitIndex)
            {
                CompletedUnits[CompletedCount] = unit;
                CompletedIndices[CompletedCount] = unitIndex;
                CompletedCount++;
            }

            internal void Clear()
            {
                CompletedCount = 0;
                Array.Clear(Cancelled, 0, Cancelled.Length);
                Array.Clear(Resolved, 0, Resolved.Length);
            }
        }
    }
}
