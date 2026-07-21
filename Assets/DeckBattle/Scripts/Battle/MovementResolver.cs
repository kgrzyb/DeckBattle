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
            PlanMovementIntents(simulation, workspace, true, true);
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
            PlanMovementIntents(simulation, workspace, false, false);
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

        private static void PlanMovementIntents(
            BattleSimulation simulation,
            Workspace workspace,
            bool updateUnitTargets,
            bool randomizeReciprocalGapConflicts)
        {
            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit == null || !unit.IsAlive || unit.IsMoving)
                {
                    continue;
                }

                UnitRuntimeState target = TargetSelector.SelectTargetOrRetainCurrent(simulation, unit, workspace.Targeting);
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

                if (!simulation.Board.TryFindPath(unit.CurrentHex, attackPosition, workspace.Path, workspace.Pathfinding, workspace.OccupiedHexes))
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
                workspace.Intents.Add(new MovementIntent(unit, target, destination, attackPosition, pathStepsToAttackPosition, arrivalTime));
            }

            ResolveReciprocalGapConflicts(simulation, workspace, randomizeReciprocalGapConflicts);
            workspace.Intents.Sort(CompareIntentPriority);
        }

        private static void ResolveReciprocalGapConflicts(
            BattleSimulation simulation,
            Workspace workspace,
            bool randomizeWinner)
        {
            Dictionary<HexCoord, DestinationContest> contests = workspace.DestinationContests;
            contests.Clear();

            for (int i = 0; i < workspace.Intents.Count; i++)
            {
                MovementIntent intent = workspace.Intents[i];
                DestinationContest contest;
                if (contests.TryGetValue(intent.Destination, out contest))
                {
                    if (contest.Count == 1)
                    {
                        contest.SecondIndex = i;
                    }

                    contest.Count++;
                    contests[intent.Destination] = contest;
                }
                else
                {
                    contests.Add(intent.Destination, new DestinationContest(i));
                }
            }

            List<int> indexesToRemove = workspace.IntentIndexesToRemove;
            indexesToRemove.Clear();

            for (int i = 0; i < workspace.Intents.Count; i++)
            {
                MovementIntent intent = workspace.Intents[i];
                DestinationContest contest = contests[intent.Destination];
                if (contest.FirstIndex != i || contest.Count != 2)
                {
                    continue;
                }

                MovementIntent first = workspace.Intents[contest.FirstIndex];
                MovementIntent second = workspace.Intents[contest.SecondIndex];
                if (!IsReciprocalOneHexGapConflict(simulation, first, second))
                {
                    continue;
                }

                bool firstWins = randomizeWinner
                    ? simulation.Random.NextInt(0, 2) == 0
                    : CompareIntentPriority(first, second) <= 0;
                indexesToRemove.Add(firstWins ? contest.SecondIndex : contest.FirstIndex);
            }

            if (indexesToRemove.Count == 0)
            {
                return;
            }

            indexesToRemove.Sort();
            for (int i = indexesToRemove.Count - 1; i >= 0; i--)
            {
                workspace.Intents.RemoveAt(indexesToRemove[i]);
            }
        }

        private static bool IsReciprocalOneHexGapConflict(
            BattleSimulation simulation,
            MovementIntent first,
            MovementIntent second)
        {
            UnitRuntimeState firstUnit = first.Unit;
            UnitRuntimeState secondUnit = second.Unit;
            if (firstUnit.Side == secondUnit.Side)
            {
                return false;
            }

            if (first.Target != secondUnit || second.Target != firstUnit)
            {
                return false;
            }

            if (first.Destination != second.Destination
                || first.AttackPosition != first.Destination
                || second.AttackPosition != second.Destination)
            {
                return false;
            }

            int firstRange = simulation.Tuning.GetAttackRange(firstUnit.Definition);
            int secondRange = simulation.Tuning.GetAttackRange(secondUnit.Definition);
            if (firstRange != 1 || secondRange != 1)
            {
                return false;
            }

            HexBoard board = simulation.Board;
            int unitDistance = board.Distance(firstUnit.CurrentHex, secondUnit.CurrentHex);
            return unitDistance == 2
                && board.Distance(firstUnit.CurrentHex, first.Destination) == 1
                && board.Distance(secondUnit.CurrentHex, second.Destination) == 1;
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

                if (!simulation.Board.TryFindPath(candidate, intent.AttackPosition, workspace.AlternativePath, workspace.AlternativePathfinding, workspace.OccupiedHexes))
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
            public readonly UnitRuntimeState Target;
            public readonly HexCoord Destination;
            public readonly HexCoord AttackPosition;
            public readonly int PathStepsToAttackPosition;
            public readonly float ArrivalTime;

            public MovementIntent(UnitRuntimeState unit, UnitRuntimeState target, HexCoord destination, HexCoord attackPosition, int pathStepsToAttackPosition, float arrivalTime)
            {
                Unit = unit;
                Target = target;
                Destination = destination;
                AttackPosition = attackPosition;
                PathStepsToAttackPosition = pathStepsToAttackPosition;
                ArrivalTime = arrivalTime;
            }
        }

        internal struct DestinationContest
        {
            public int FirstIndex;
            public int SecondIndex;
            public int Count;

            public DestinationContest(int firstIndex)
            {
                FirstIndex = firstIndex;
                SecondIndex = -1;
                Count = 1;
            }
        }

        public sealed class Workspace
        {
            internal readonly HashSet<HexCoord> OccupiedHexes;
            internal readonly Dictionary<HexCoord, int> ReservedHexes;
            internal readonly Dictionary<HexCoord, DestinationContest> DestinationContests;
            internal readonly List<MovementIntent> Intents;
            internal readonly List<int> IntentIndexesToRemove;
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
                DestinationContests = new Dictionary<HexCoord, DestinationContest>(units);
                Intents = new List<MovementIntent>(units);
                IntentIndexesToRemove = new List<int>(units);
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
                DestinationContests.Clear();
                Intents.Clear();
                IntentIndexesToRemove.Clear();
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
