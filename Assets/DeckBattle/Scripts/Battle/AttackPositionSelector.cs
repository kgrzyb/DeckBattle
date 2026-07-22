using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class AttackPositionSelector
    {
        public static bool TrySelectAttackPosition(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            out HexCoord attackPosition)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            var workspace = new Workspace(simulation.Board.Width * simulation.Board.Height);
            bool found = TrySelectAttackPosition(simulation, attacker, target, workspace, out AttackPathResult result);
            attackPosition = result.AttackPosition;
            return found;
        }

        public static bool TrySelectAttackPosition(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            Workspace workspace,
            out HexCoord attackPosition)
        {
            bool found = TrySelectAttackPosition(simulation, attacker, target, workspace, out AttackPathResult result);
            attackPosition = result.AttackPosition;
            return found;
        }

        public static bool TrySelectAttackPosition(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            Workspace workspace,
            out AttackPathResult result)
        {
            return TrySelectAttackPosition(simulation, attacker, target, workspace, null, out result);
        }

        public static bool TrySelectAttackPosition(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            Workspace workspace,
            HashSet<HexCoord> additionalBlockedHexes,
            out AttackPathResult result)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            result = default;
            if (!attacker.IsAlive || !target.IsAlive || attacker.Side == target.Side)
            {
                return false;
            }

            workspace.Clear();
            FillOccupiedHexes(simulation.Units, workspace.OccupiedHexes);
            foreach (HexCoord occupiedHex in workspace.OccupiedHexes)
            {
                workspace.DynamicBlockedHexes.Add(occupiedHex);
            }

            if (additionalBlockedHexes != null)
            {
                foreach (HexCoord blockedHex in additionalBlockedHexes)
                {
                    workspace.DynamicBlockedHexes.Add(blockedHex);
                }
            }

            HexBoard board = simulation.Board;
            int attackRange = simulation.Tuning.GetAttackRange(attacker.Definition);
            if (board.Distance(attacker.CurrentHex, target.CurrentHex) <= attackRange)
            {
                result = new AttackPathResult(attacker.CurrentHex, attacker.CurrentHex, 0, true);
                return true;
            }

            // A moving target still occupies its logical hex. This compatibility check
            // also lets callers query a target's pending destination explicitly.
            if (target.IsMoving && board.Distance(attacker.CurrentHex, target.MovementDestination) <= attackRange)
            {
                result = new AttackPathResult(attacker.CurrentHex, attacker.CurrentHex, 0, true);
                return true;
            }

            board.FillHexesInRange(target.CurrentHex, attackRange, workspace.AttackPositions);
            for (int i = workspace.AttackPositions.Count - 1; i >= 0; i--)
            {
                HexCoord candidate = workspace.AttackPositions[i];
                if (!IsCandidateAttackPosition(board, attacker, target, candidate, attackRange, workspace))
                {
                    workspace.AttackPositions.RemoveAt(i);
                }
            }

            if (workspace.AttackPositions.Count == 0
                || !board.TryFindShortestPathToAny(
                    attacker.CurrentHex,
                    workspace.AttackPositions,
                    workspace.Path,
                    workspace.Pathfinding,
                    workspace.DynamicBlockedHexes,
                    out HexCoord selectedPosition,
                    out HexCoord nextStep,
                    out int pathSteps))
            {
                return false;
            }

            result = new AttackPathResult(selectedPosition, nextStep, pathSteps, false);
            return true;
        }

        private static bool IsCandidateAttackPosition(
            HexBoard board,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            HexCoord candidate,
            int attackRange,
            Workspace workspace)
        {
            if (!board.IsWalkable(candidate)
                || board.Distance(candidate, target.CurrentHex) > attackRange
                || candidate == target.CurrentHex)
            {
                return false;
            }

            if (candidate != attacker.CurrentHex
                && (workspace.OccupiedHexes.Contains(candidate)
                    || workspace.DynamicBlockedHexes.Contains(candidate)))
            {
                return false;
            }

            return true;
        }

        private static void FillOccupiedHexes(IReadOnlyList<UnitRuntimeState> units, HashSet<HexCoord> occupiedHexes)
        {
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                occupiedHexes.Add(unit.CurrentHex);
                if (unit.IsMoving)
                {
                    occupiedHexes.Add(unit.MovementDestination);
                }
            }
        }

        public readonly struct AttackPathResult
        {
            public readonly HexCoord AttackPosition;
            public readonly HexCoord NextStep;
            public readonly int PathSteps;
            public readonly bool IsAlreadyInRange;

            public AttackPathResult(HexCoord attackPosition, HexCoord nextStep, int pathSteps, bool isAlreadyInRange)
            {
                AttackPosition = attackPosition;
                NextStep = nextStep;
                PathSteps = pathSteps;
                IsAlreadyInRange = isAlreadyInRange;
            }

            public bool AlreadyInRange
            {
                get { return IsAlreadyInRange; }
            }

            public bool IsInRange
            {
                get { return IsAlreadyInRange; }
            }

            public int PathLength
            {
                get { return PathSteps; }
            }
        }

        public sealed class Workspace
        {
            internal readonly HashSet<HexCoord> OccupiedHexes;
            internal readonly HashSet<HexCoord> DynamicBlockedHexes;
            internal readonly List<HexCoord> AttackPositions;
            internal readonly List<HexCoord> Path;
            internal readonly HexBoard.PathfindingWorkspace Pathfinding;

            public Workspace(int boardCellCapacity)
            {
                int capacity = Math.Max(1, boardCellCapacity);
                OccupiedHexes = new HashSet<HexCoord>(capacity);
                DynamicBlockedHexes = new HashSet<HexCoord>(capacity);
                AttackPositions = new List<HexCoord>(capacity);
                Path = new List<HexCoord>(capacity);
                Pathfinding = new HexBoard.PathfindingWorkspace(capacity);
            }

            internal void Clear()
            {
                OccupiedHexes.Clear();
                DynamicBlockedHexes.Clear();
                AttackPositions.Clear();
                Path.Clear();
                Pathfinding.Clear();
            }
        }
    }
}
