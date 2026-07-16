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
            PlanMovementIntents(simulation, workspace, true);
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
            PlanMovementIntents(simulation, workspace, false);
            return ReservePlannedDestinations(simulation, workspace, destinationsByUnitId);
        }

        private static void AdvanceActiveMovements(BattleSimulation simulation, float tickDuration)
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

        private static void PlanMovementIntents(BattleSimulation simulation, Workspace workspace, bool updateUnitTargets)
        {
            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit == null || !unit.IsAlive || unit.IsMoving)
                {
                    continue;
                }

                UnitRuntimeState target = TargetSelector.SelectTarget(simulation, unit, workspace.Targeting);
                if (target == null)
                {
                    if (updateUnitTargets)
                    {
                        unit.ClearTarget();
                    }

                    continue;
                }

                if (updateUnitTargets)
                {
                    unit.SetTarget(target);
                }

                HexCoord attackPosition;
                if (!AttackPositionSelector.TrySelectAttackPosition(simulation, unit, target, workspace.AttackPosition, out attackPosition))
                {
                    continue;
                }

                if (attackPosition == unit.CurrentHex)
                {
                    continue;
                }

                if (!simulation.Board.TryFindPath(unit.CurrentHex, attackPosition, workspace.Path, workspace.Pathfinding))
                {
                    continue;
                }

                if (workspace.Path.Count < 2)
                {
                    continue;
                }

                HexCoord destination = workspace.Path[1];
                if (!CanEndMoveOn(unit, destination, workspace.OccupiedHexes))
                {
                    continue;
                }

                int pathStepsToAttackPosition = workspace.Path.Count - 1;
                float arrivalTime = pathStepsToAttackPosition * simulation.Tuning.MovementStepDuration;
                workspace.Intents.Add(new MovementIntent(unit, destination, attackPosition, pathStepsToAttackPosition, arrivalTime));
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
                    if (!TryFindAlternativeStep(simulation, intent, workspace, out destination))
                    {
                        continue;
                    }
                }

                HexCoord from = intent.Unit.CurrentHex;
                workspace.ReservedHexes.Add(destination, intent.Unit.UnitId);
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
                    if (!TryFindAlternativeStep(simulation, intent, workspace, out destination))
                    {
                        continue;
                    }
                }

                workspace.ReservedHexes.Add(destination, intent.Unit.UnitId);
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
            workspace.Neighbors.Clear();
            simulation.Board.FillNeighbors(intent.Unit.CurrentHex, workspace.Neighbors);
            workspace.Neighbors.Sort(CompareHexCoords);

            bool found = false;
            int bestDistance = int.MaxValue;
            HexCoord bestDestination = default;

            for (int i = 0; i < workspace.Neighbors.Count; i++)
            {
                HexCoord candidate = workspace.Neighbors[i];
                if (!CanReserveDestination(intent.Unit, candidate, workspace.OccupiedHexes, workspace.ReservedHexes))
                {
                    continue;
                }

                if (!simulation.Board.TryFindPath(candidate, intent.AttackPosition, workspace.AlternativePath, workspace.AlternativePathfinding))
                {
                    continue;
                }

                int distance = workspace.AlternativePath.Count - 1;
                if (!found || distance < bestDistance || (distance == bestDistance && CompareHexCoords(candidate, bestDestination) < 0))
                {
                    found = true;
                    bestDistance = distance;
                    bestDestination = candidate;
                }
            }

            if (!found)
            {
                return false;
            }

            destination = bestDestination;
            return true;
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
            int arrivalCompare = left.ArrivalTime.CompareTo(right.ArrivalTime);
            if (arrivalCompare != 0)
            {
                return arrivalCompare;
            }

            int pathCompare = left.PathStepsToAttackPosition.CompareTo(right.PathStepsToAttackPosition);
            if (pathCompare != 0)
            {
                return pathCompare;
            }

            return left.Unit.UnitId.CompareTo(right.Unit.UnitId);
        }

        private static int CompareHexCoords(HexCoord left, HexCoord right)
        {
            int qCompare = left.Q.CompareTo(right.Q);
            if (qCompare != 0)
            {
                return qCompare;
            }

            return left.R.CompareTo(right.R);
        }

        internal readonly struct MovementIntent
        {
            public readonly UnitRuntimeState Unit;
            public readonly HexCoord Destination;
            public readonly HexCoord AttackPosition;
            public readonly int PathStepsToAttackPosition;
            public readonly float ArrivalTime;

            public MovementIntent(UnitRuntimeState unit, HexCoord destination, HexCoord attackPosition, int pathStepsToAttackPosition, float arrivalTime)
            {
                Unit = unit;
                Destination = destination;
                AttackPosition = attackPosition;
                PathStepsToAttackPosition = pathStepsToAttackPosition;
                ArrivalTime = arrivalTime;
            }
        }

        public sealed class Workspace
        {
            internal readonly HashSet<HexCoord> OccupiedHexes;
            internal readonly Dictionary<HexCoord, int> ReservedHexes;
            internal readonly List<MovementIntent> Intents;
            internal readonly List<HexCoord> Path;
            internal readonly List<HexCoord> AlternativePath;
            internal readonly List<HexCoord> Neighbors;
            internal readonly HexBoard.PathfindingWorkspace Pathfinding;
            internal readonly HexBoard.PathfindingWorkspace AlternativePathfinding;
            internal readonly TargetSelector.Workspace Targeting;
            internal readonly AttackPositionSelector.Workspace AttackPosition;

            public Workspace(int boardCellCapacity, int unitCapacity)
            {
                int boardCapacity = Math.Max(1, boardCellCapacity);
                int units = Math.Max(1, unitCapacity);
                OccupiedHexes = new HashSet<HexCoord>();
                ReservedHexes = new Dictionary<HexCoord, int>(units);
                Intents = new List<MovementIntent>(units);
                Path = new List<HexCoord>(boardCapacity);
                AlternativePath = new List<HexCoord>(boardCapacity);
                Neighbors = new List<HexCoord>(6);
                Pathfinding = new HexBoard.PathfindingWorkspace(boardCapacity);
                AlternativePathfinding = new HexBoard.PathfindingWorkspace(boardCapacity);
                Targeting = new TargetSelector.Workspace(boardCapacity);
                AttackPosition = new AttackPositionSelector.Workspace(boardCapacity);
            }

            internal void Clear()
            {
                OccupiedHexes.Clear();
                ReservedHexes.Clear();
                Intents.Clear();
                Path.Clear();
                AlternativePath.Clear();
                Neighbors.Clear();
                Pathfinding.Clear();
                AlternativePathfinding.Clear();
                Targeting.Clear();
                AttackPosition.Clear();
            }
        }
    }
}
