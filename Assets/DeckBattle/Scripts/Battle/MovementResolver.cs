using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class MovementResolver
    {
        public static int ResolveMovement(BattleSimulation simulation)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            var workspace = new Workspace(simulation.Board.Width * simulation.Board.Height, simulation.Units.Count);
            return ResolveMovement(simulation, 1f, workspace, null);
        }

        public static int ResolveMovement(BattleSimulation simulation, Workspace workspace)
        {
            return ResolveMovement(simulation, 1f, workspace, null);
        }

        public static int ResolveMovement(BattleSimulation simulation, float tickDuration, Workspace workspace, BattleEventQueue eventQueue)
        {
            return ResolveMovement(simulation, tickDuration, workspace, eventQueue, null, null);
        }

        public static int ResolveMovement(
            BattleSimulation simulation,
            float tickDuration,
            Workspace workspace,
            BattleEventQueue eventQueue,
            TargetSelector.TargetSelection[] targetSelections,
            bool[] targetSelectionValid)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            workspace.Clear();
            AdvanceActiveMovements(simulation, tickDuration);
            FillOccupiedHexes(simulation.Units, workspace.OccupiedHexes);
            PlanMovementIntents(simulation, workspace, true, targetSelections, targetSelectionValid);
            return ApplyMovementIntents(simulation, workspace, eventQueue);
        }

        public static int ResolveMovement(BattleSimulation simulation, Workspace workspace, BattleEventQueue eventQueue)
        {
            return ResolveMovement(simulation, 1f, workspace, eventQueue);
        }

        public static int PlanMovementDestinations(
            BattleSimulation simulation,
            Workspace workspace,
            Dictionary<int, HexCoord> destinationsByUnitId)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (destinationsByUnitId == null)
            {
                throw new ArgumentNullException(nameof(destinationsByUnitId));
            }

            workspace.Clear();
            destinationsByUnitId.Clear();
            FillOccupiedHexes(simulation.Units, workspace.OccupiedHexes);
            PlanMovementIntents(simulation, workspace, false, null, null);
            return ReservePlannedDestinations(simulation, workspace, destinationsByUnitId);
        }

        public static void AdvanceActiveMovements(BattleSimulation simulation, float tickDuration)
        {
            if (tickDuration <= 0f)
            {
                return;
            }

            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit == null || !unit.IsAlive || !unit.IsMoving)
                {
                    continue;
                }

                unit.MovementTimeRemaining = Math.Max(0f, unit.MovementTimeRemaining - tickDuration);
                if (unit.MovementTimeRemaining <= 0f)
                {
                    simulation.CompleteUnitMovement(unit);
                }
            }
        }

        private static void PlanMovementIntents(
            BattleSimulation simulation,
            Workspace workspace,
            bool updateUnitTargets,
            TargetSelector.TargetSelection[] targetSelections,
            bool[] targetSelectionValid)
        {
            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit == null || !unit.IsAlive || unit.IsMoving)
                {
                    continue;
                }

                TargetSelector.TargetSelection selection;
                bool hasSelection = targetSelections != null
                    && targetSelectionValid != null
                    && i < targetSelections.Length
                    && i < targetSelectionValid.Length
                    && targetSelectionValid[i];
                if (hasSelection)
                {
                    selection = targetSelections[i];
                }
                else if (!TargetSelector.TrySelectTargetOrRetainCurrent(
                             simulation,
                             unit,
                             workspace.Targeting,
                             out selection))
                {
                    if (updateUnitTargets)
                    {
                        unit.ClearTarget();
                    }

                    continue;
                }

                UnitRuntimeState target = selection.Target;

                if (updateUnitTargets)
                {
                    unit.SetTarget(target);
                }

                AttackPositionSelector.AttackPathResult attackPath = selection.AttackPath;
                if (attackPath.IsAlreadyInRange || attackPath.NextStep == unit.CurrentHex)
                {
                    continue;
                }

                HexCoord destination = attackPath.NextStep;
                if (!CanEndMoveOn(unit, destination, workspace.OccupiedHexes))
                {
                    continue;
                }

                workspace.Intents.Add(new MovementIntent(
                    unit,
                    target,
                    destination,
                    attackPath.AttackPosition,
                    attackPath.PathSteps));
            }

            workspace.Intents.Sort(CompareIntentPriority);
        }

        private static int ApplyMovementIntents(BattleSimulation simulation, Workspace workspace, BattleEventQueue eventQueue)
        {
            int movedCount = 0;
            for (int i = 0; i < workspace.Intents.Count; i++)
            {
                MovementIntent intent = workspace.Intents[i];
                if (!intent.Unit.IsAlive)
                {
                    continue;
                }

                HexCoord destination = intent.Destination;
                if (!CanReserveDestination(intent.Unit, destination, workspace.OccupiedHexes, workspace.ReservedHexes))
                {
                    if (IsReciprocalConflict(simulation, intent, destination, workspace))
                    {
                        continue;
                    }

                    if (!TryFindAlternativeStep(simulation, intent, workspace, out destination))
                    {
                        continue;
                    }
                }

                HexCoord from = intent.Unit.CurrentHex;
                workspace.ReservedHexes.Add(destination, intent.Unit.UnitId);
                workspace.ReservedIntentTargets.Add(destination, intent.Target);
                workspace.ReservedBlockedHexes.Add(destination);
                workspace.OccupiedHexes.Add(destination);
                simulation.StartUnitMovement(intent.Unit, destination);
                if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.UnitMoved(intent.Unit.UnitId, from, destination));
                }

                movedCount++;
            }

            return movedCount;
        }

        private static int ReservePlannedDestinations(
            BattleSimulation simulation,
            Workspace workspace,
            Dictionary<int, HexCoord> destinationsByUnitId)
        {
            int plannedCount = 0;
            for (int i = 0; i < workspace.Intents.Count; i++)
            {
                MovementIntent intent = workspace.Intents[i];
                if (!intent.Unit.IsAlive)
                {
                    continue;
                }

                HexCoord destination = intent.Destination;
                if (!CanReserveDestination(intent.Unit, destination, workspace.OccupiedHexes, workspace.ReservedHexes))
                {
                    if (IsReciprocalConflict(simulation, intent, destination, workspace))
                    {
                        continue;
                    }

                    if (!TryFindAlternativeStep(simulation, intent, workspace, out destination))
                    {
                        continue;
                    }
                }

                workspace.ReservedHexes.Add(destination, intent.Unit.UnitId);
                workspace.ReservedIntentTargets.Add(destination, intent.Target);
                workspace.ReservedBlockedHexes.Add(destination);
                workspace.OccupiedHexes.Add(destination);
                destinationsByUnitId[intent.Unit.UnitId] = destination;
                plannedCount++;
            }

            return plannedCount;
        }

        private static bool TryFindAlternativeStep(
            BattleSimulation simulation,
            MovementIntent intent,
            Workspace workspace,
            out HexCoord destination)
        {
            destination = default;
            if (!AttackPositionSelector.TrySelectAttackPosition(
                    simulation,
                    intent.Unit,
                    intent.Target,
                    workspace.AttackPosition,
                    workspace.ReservedBlockedHexes,
                    out AttackPositionSelector.AttackPathResult alternativePath)
                || alternativePath.IsAlreadyInRange
                || alternativePath.NextStep == intent.Unit.CurrentHex
                || !CanReserveDestination(
                    intent.Unit,
                    alternativePath.NextStep,
                    workspace.OccupiedHexes,
                    workspace.ReservedHexes))
            {
                return false;
            }

            destination = alternativePath.NextStep;
            return true;
        }

        private static bool IsReciprocalConflict(
            BattleSimulation simulation,
            MovementIntent intent,
            HexCoord destination,
            Workspace workspace)
        {
            if (!workspace.ReservedHexes.TryGetValue(destination, out int winnerId)
                || !simulation.TryGetUnitById(winnerId, out UnitRuntimeState winner)
                || !workspace.ReservedIntentTargets.TryGetValue(destination, out UnitRuntimeState winnerTarget))
            {
                return false;
            }

            return winner != null
                && winner.Side != intent.Unit.Side
                && intent.Target == winner
                && winnerTarget == intent.Unit;
        }

        private static bool CanEndMoveOn(UnitRuntimeState unit, HexCoord destination, HashSet<HexCoord> occupiedHexes)
        {
            return destination == unit.CurrentHex || !occupiedHexes.Contains(destination);
        }

        private static bool CanReserveDestination(
            UnitRuntimeState unit,
            HexCoord destination,
            HashSet<HexCoord> occupiedHexes,
            Dictionary<HexCoord, int> reservedHexes)
        {
            return destination != unit.CurrentHex
                && !occupiedHexes.Contains(destination)
                && !reservedHexes.ContainsKey(destination);
        }

        private static void FillOccupiedHexes(IReadOnlyList<UnitRuntimeState> units, HashSet<HexCoord> occupiedHexes)
        {
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit != null && unit.IsAlive)
                {
                    occupiedHexes.Add(unit.CurrentHex);
                    if (unit.IsMoving)
                    {
                        occupiedHexes.Add(unit.MovementDestination);
                    }
                }
            }
        }

        private static int CompareIntentPriority(MovementIntent left, MovementIntent right)
        {
            int pathCompare = left.PathStepsToAttackPosition.CompareTo(right.PathStepsToAttackPosition);
            if (pathCompare != 0)
            {
                return pathCompare;
            }

            return left.Unit.UnitId.CompareTo(right.Unit.UnitId);
        }

        internal readonly struct MovementIntent
        {
            public readonly UnitRuntimeState Unit;
            public readonly UnitRuntimeState Target;
            public readonly HexCoord Destination;
            public readonly HexCoord AttackPosition;
            public readonly int PathStepsToAttackPosition;

            public MovementIntent(UnitRuntimeState unit, UnitRuntimeState target, HexCoord destination, HexCoord attackPosition, int pathStepsToAttackPosition)
            {
                Unit = unit;
                Target = target;
                Destination = destination;
                AttackPosition = attackPosition;
                PathStepsToAttackPosition = pathStepsToAttackPosition;
            }
        }

        public sealed class Workspace
        {
            internal readonly HashSet<HexCoord> OccupiedHexes;
            internal readonly Dictionary<HexCoord, int> ReservedHexes;
            internal readonly Dictionary<HexCoord, UnitRuntimeState> ReservedIntentTargets;
            internal readonly HashSet<HexCoord> ReservedBlockedHexes;
            internal readonly List<MovementIntent> Intents;
            internal readonly TargetSelector.Workspace Targeting;
            internal readonly AttackPositionSelector.Workspace AttackPosition;

            public Workspace(int boardCellCapacity, int unitCapacity)
            {
                int boardCapacity = Math.Max(1, boardCellCapacity);
                int units = Math.Max(1, unitCapacity);
                OccupiedHexes = new HashSet<HexCoord>(boardCapacity);
                ReservedHexes = new Dictionary<HexCoord, int>(units);
                ReservedIntentTargets = new Dictionary<HexCoord, UnitRuntimeState>(units);
                ReservedBlockedHexes = new HashSet<HexCoord>(units);
                Intents = new List<MovementIntent>(units);
                Targeting = new TargetSelector.Workspace(boardCapacity);
                AttackPosition = new AttackPositionSelector.Workspace(boardCapacity);
            }

            internal void Clear()
            {
                OccupiedHexes.Clear();
                ReservedHexes.Clear();
                ReservedIntentTargets.Clear();
                ReservedBlockedHexes.Clear();
                Intents.Clear();
                Targeting.Clear();
                AttackPosition.Clear();
            }
        }
    }
}
