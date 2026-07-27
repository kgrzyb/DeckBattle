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

            return ResolveMovement(simulation, new Workspace(simulation.Board.Width * simulation.Board.Height, simulation.Units.Count));
        }

        public static int ResolveMovement(BattleSimulation simulation, Workspace workspace)
        {
            return ResolveMovement(simulation, 0f, workspace, null);
        }

        // tickDuration remains for source compatibility. Logical movement is committed in this call.
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

            PrepareTargetSelections(simulation, workspace);
            return ResolveMovement(simulation, tickDuration, workspace, eventQueue, workspace.TargetSelections, workspace.TargetSelectionValid);
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

            if (targetSelections == null || targetSelectionValid == null)
            {
                throw new ArgumentNullException("Prepared target selections are required for movement collection.");
            }

            workspace.Clear();
            CollectIntents(simulation, workspace, targetSelections, targetSelectionValid);
            ResolveConflicts(workspace);
            return CommitWinners(simulation, workspace, eventQueue);
        }

        public static void AdvanceActiveMovements(BattleSimulation simulation, float tickDuration)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (tickDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDuration));
            }

            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                simulation.AdvanceMovement(units[i], tickDuration);
            }
        }

        public static int ResolveMovement(BattleSimulation simulation, Workspace workspace, BattleEventQueue eventQueue)
        {
            return ResolveMovement(simulation, 0f, workspace, eventQueue);
        }

        public static int PlanMovementDestinations(
            BattleSimulation simulation,
            Workspace workspace,
            Dictionary<int, HexCoord> destinationsByUnitId)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (destinationsByUnitId == null) throw new ArgumentNullException(nameof(destinationsByUnitId));

            PrepareTargetSelections(simulation, workspace);
            workspace.Clear();
            destinationsByUnitId.Clear();
            CollectIntents(simulation, workspace, workspace.TargetSelections, workspace.TargetSelectionValid);
            ResolveConflicts(workspace);
            for (int i = 0; i < workspace.Winners.Count; i++)
            {
                MovementIntent winner = workspace.Winners[i];
                destinationsByUnitId[winner.Unit.UnitId] = winner.Destination;
            }

            return workspace.Winners.Count;
        }

        private static void PrepareTargetSelections(BattleSimulation simulation, Workspace workspace)
        {
            workspace.EnsureUnitCapacity(simulation.Units.Count);
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                workspace.TargetSelectionValid[i] = false;
                UnitRuntimeState unit = simulation.Units[i];
                if (unit != null && unit.IsAlive
                    && TargetSelector.TrySelectTargetOrRetainCurrent(simulation, unit, workspace.Targeting, out TargetSelector.TargetSelection selection))
                {
                    workspace.TargetSelections[i] = selection;
                    workspace.TargetSelectionValid[i] = true;
                }
            }
        }

        private static void CollectIntents(
            BattleSimulation simulation,
            Workspace workspace,
            TargetSelector.TargetSelection[] targetSelections,
            bool[] targetSelectionValid)
        {
            FillOccupiedHexes(simulation.Units, workspace.OccupiedAtCollectStart);
            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            int count = Math.Min(units.Count, Math.Min(targetSelections.Length, targetSelectionValid.Length));
            for (int i = 0; i < count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit == null
                    || !unit.IsAlive
                    || unit.IsMoving
                    // Once the hit has been committed, the remaining attack cooldown
                    // must not leave a unit standing still after its target dies.
                    || unit.AttackPhase == UnitAttackPhase.Windup
                    || !targetSelectionValid[i])
                {
                    continue;
                }

                TargetSelector.TargetSelection selection = targetSelections[i];
                AttackPositionSelector.AttackPathResult path = selection.AttackPath;
                HexCoord destination = path.NextStep;
                if (!selection.HasTarget || path.IsAlreadyInRange || destination == unit.CurrentHex
                    || simulation.Board.Distance(unit.CurrentHex, destination) != 1
                    || !simulation.Board.IsWalkable(destination)
                    || workspace.OccupiedAtCollectStart.Contains(destination))
                {
                    continue;
                }

                workspace.DesiredMoves.Add(new MovementIntent(unit, destination));
            }
        }

        private static void ResolveConflicts(Workspace workspace)
        {
            for (int i = 0; i < workspace.DesiredMoves.Count; i++)
            {
                MovementIntent candidate = workspace.DesiredMoves[i];
                if (!workspace.WinnerByDestination.TryGetValue(candidate.Destination, out MovementIntent winner)
                    || IsDeployedEarlier(candidate.Unit, winner.Unit))
                {
                    workspace.WinnerByDestination[candidate.Destination] = candidate;
                }
            }

            for (int i = 0; i < workspace.DesiredMoves.Count; i++)
            {
                MovementIntent intent = workspace.DesiredMoves[i];
                if (workspace.WinnerByDestination.TryGetValue(intent.Destination, out MovementIntent winner)
                    && winner.Unit == intent.Unit)
                {
                    workspace.Winners.Add(intent);
                }
            }
        }

        // Runtime UnitId is allocated when a unit is played; lower means deployed earlier.
        private static bool IsDeployedEarlier(UnitRuntimeState candidate, UnitRuntimeState currentWinner)
        {
            return candidate.UnitId < currentWinner.UnitId;
        }

        private static int CommitWinners(BattleSimulation simulation, Workspace workspace, BattleEventQueue eventQueue)
        {
            for (int i = 0; i < workspace.Winners.Count; i++)
            {
                MovementIntent winner = workspace.Winners[i];
                if (!winner.Unit.IsAlive)
                {
                    continue;
                }

                HexCoord from = winner.Unit.CurrentHex;
                simulation.StartUnitMovement(winner.Unit, winner.Destination);
                if (eventQueue != null)
                {
                    eventQueue.Enqueue(BattleEvent.UnitMoved(winner.Unit.UnitId, from, winner.Destination));
                }
            }

            return workspace.Winners.Count;
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

        internal readonly struct MovementIntent
        {
            public readonly UnitRuntimeState Unit;
            public readonly HexCoord Destination;

            public MovementIntent(UnitRuntimeState unit, HexCoord destination)
            {
                Unit = unit;
                Destination = destination;
            }
        }

        public sealed class Workspace
        {
            internal readonly HashSet<HexCoord> OccupiedAtCollectStart;
            internal readonly Dictionary<HexCoord, MovementIntent> WinnerByDestination;
            internal readonly List<MovementIntent> DesiredMoves;
            internal readonly List<MovementIntent> Winners;
            internal readonly TargetSelector.Workspace Targeting;
            internal TargetSelector.TargetSelection[] TargetSelections;
            internal bool[] TargetSelectionValid;

            public Workspace(int boardCellCapacity, int unitCapacity)
            {
                int boardCapacity = Math.Max(1, boardCellCapacity);
                int capacity = Math.Max(1, unitCapacity);
                OccupiedAtCollectStart = new HashSet<HexCoord>(boardCapacity);
                WinnerByDestination = new Dictionary<HexCoord, MovementIntent>(capacity);
                DesiredMoves = new List<MovementIntent>(capacity);
                Winners = new List<MovementIntent>(capacity);
                Targeting = new TargetSelector.Workspace(boardCapacity);
                TargetSelections = new TargetSelector.TargetSelection[capacity];
                TargetSelectionValid = new bool[capacity];
            }

            internal void EnsureUnitCapacity(int unitCount)
            {
                if (TargetSelections.Length < unitCount)
                {
                    Array.Resize(ref TargetSelections, unitCount);
                    Array.Resize(ref TargetSelectionValid, unitCount);
                }
            }

            internal void Clear()
            {
                OccupiedAtCollectStart.Clear();
                WinnerByDestination.Clear();
                DesiredMoves.Clear();
                Winners.Clear();
                Targeting.Clear();
            }
        }
    }
}
