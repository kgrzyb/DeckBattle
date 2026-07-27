using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class TargetSelector
    {
        public static UnitRuntimeState SelectTarget(BattleSimulation simulation, UnitRuntimeState attacker)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (!attacker.IsAlive)
            {
                return null;
            }

            var workspace = new Workspace(simulation.Board.Width * simulation.Board.Height);
            return SelectTarget(simulation, attacker, workspace);
        }

        public static UnitRuntimeState SelectTarget(BattleSimulation simulation, UnitRuntimeState attacker, Workspace workspace)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (!attacker.IsAlive)
            {
                return null;
            }

            if (!TrySelectTarget(simulation, attacker, workspace, out TargetSelection selection))
            {
                return null;
            }

            return selection.Target;
        }

        public static bool TrySelectTarget(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            Workspace workspace,
            out TargetSelection selection)
        {
            return TrySelectTarget(
                simulation,
                attacker,
                workspace,
                UnitRuntimeState.NoTargetUnitId,
                out selection);
        }

        public static bool TrySelectTarget(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            Workspace workspace,
            int preferredTargetUnitId,
            out TargetSelection selection)
        {
            ValidateArguments(simulation, attacker, workspace);
            if (!attacker.IsAlive)
            {
                selection = default;
                return false;
            }

            workspace.Clear();
            return TrySelectTargetByPath(simulation, attacker, workspace, preferredTargetUnitId, out selection);
        }

        public static TargetSelection SelectTargetSelection(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            Workspace workspace)
        {
            if (!TrySelectTarget(simulation, attacker, workspace, out TargetSelection selection))
            {
                return default;
            }

            return selection;
        }

        private static bool TrySelectTargetByPath(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            Workspace workspace,
            int preferredTargetUnitId,
            out TargetSelection selection)
        {
            if (!attacker.IsAlive)
            {
                selection = default;
                return false;
            }

            HexBoard board = simulation.Board;
            if (!board.IsWalkable(attacker.CurrentHex))
            {
                selection = default;
                return false;
            }

            AttackPositionSelector.Workspace attackWorkspace = workspace.AttackPosition;
            FillDynamicBlockedHexes(simulation, attackWorkspace.OccupiedHexes, attackWorkspace.DynamicBlockedHexes);

            HexBoard.PathfindingWorkspace pathfinding = attackWorkspace.Pathfinding;
            Dictionary<HexCoord, HexCoord> cameFrom = pathfinding.CameFrom;
            List<HexCoord> frontier = pathfinding.Frontier;
            List<HexCoord> neighbors = pathfinding.Neighbors;
            cameFrom.Add(attacker.CurrentHex, attacker.CurrentHex);
            frontier.Add(attacker.CurrentHex);

            int readIndex = 0;
            int levelEnd = frontier.Count;
            int pathSteps = 0;
            while (readIndex < frontier.Count)
            {
                UnitRuntimeState levelTarget = null;
                HexCoord levelAttackPosition = default;

                while (readIndex < levelEnd)
                {
                    HexCoord current = frontier[readIndex];
                    readIndex++;

                    UnitRuntimeState targetAtPosition = SelectTargetInAttackRange(
                        simulation,
                        attacker,
                        current,
                        preferredTargetUnitId,
                        out int targetDistance,
                        out int targetHp);
                    if (targetAtPosition != null
                        && IsBetterEncounter(
                            targetAtPosition,
                            targetDistance,
                            targetHp,
                            current,
                            levelTarget,
                            levelAttackPosition,
                            preferredTargetUnitId,
                            board))
                    {
                        levelTarget = targetAtPosition;
                        levelAttackPosition = current;
                    }

                    neighbors.Clear();
                    board.FillNeighbors(current, neighbors);
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        HexCoord neighbor = neighbors[i];
                        if (!board.IsWalkable(neighbor)
                            || (neighbor != attacker.CurrentHex
                                && attackWorkspace.DynamicBlockedHexes.Contains(neighbor))
                            || cameFrom.ContainsKey(neighbor))
                        {
                            continue;
                        }

                        cameFrom.Add(neighbor, current);
                        frontier.Add(neighbor);
                    }
                }

                if (levelTarget != null)
                {
                    BuildPath(
                        attacker.CurrentHex,
                        levelAttackPosition,
                        attackWorkspace.Path,
                        pathfinding.ReversedPath,
                        cameFrom);

                    HexCoord nextStep = attackWorkspace.Path.Count > 1
                        ? attackWorkspace.Path[1]
                        : attacker.CurrentHex;
                    selection = new TargetSelection(
                        levelTarget,
                        new AttackPositionSelector.AttackPathResult(
                            levelAttackPosition,
                            nextStep,
                            pathSteps,
                            pathSteps == 0));
                    return true;
                }

                pathSteps++;
                levelEnd = frontier.Count;
            }

            selection = default;
            return false;
        }

        private static UnitRuntimeState SelectTargetInAttackRange(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            HexCoord attackPosition,
            int preferredTargetUnitId,
            out int targetDistance,
            out int targetHp)
        {
            UnitRuntimeState selected = null;
            targetDistance = int.MaxValue;
            targetHp = int.MaxValue;
            int selectedUnitId = int.MaxValue;
            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            int attackRange = simulation.Tuning.GetAttackRange(attacker.Definition);
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState candidate = units[i];
                if (!TargetingRules.CanBeTargeted(attacker, candidate))
                {
                    continue;
                }

                int distance = simulation.Board.Distance(
                    attackPosition,
                    AttackPositionSelector.GetTargetPlanningHex(candidate));
                if (distance > attackRange)
                {
                    continue;
                }

                bool candidateIsPreferred = candidate.UnitId == preferredTargetUnitId;
                bool selectedIsPreferred = selected != null && selected.UnitId == preferredTargetUnitId;
                if (selected == null
                    || (candidateIsPreferred && !selectedIsPreferred)
                    || (!candidateIsPreferred
                        && !selectedIsPreferred
                        && (distance < targetDistance
                            || (distance == targetDistance && candidate.CurrentHp < targetHp)
                            || (distance == targetDistance
                                && candidate.CurrentHp == targetHp
                                && candidate.UnitId < selectedUnitId))))
                {
                    selected = candidate;
                    targetDistance = distance;
                    targetHp = candidate.CurrentHp;
                    selectedUnitId = candidate.UnitId;
                }
            }

            return selected;
        }

        private static bool IsBetterEncounter(
            UnitRuntimeState candidate,
            int candidateTargetDistance,
            int candidateHp,
            HexCoord candidateAttackPosition,
            UnitRuntimeState selected,
            HexCoord selectedAttackPosition,
            int preferredTargetUnitId,
            HexBoard board)
        {
            if (selected == null)
            {
                return true;
            }

            bool candidateIsPreferred = candidate.UnitId == preferredTargetUnitId;
            bool selectedIsPreferred = selected.UnitId == preferredTargetUnitId;
            if (candidateIsPreferred != selectedIsPreferred)
            {
                return candidateIsPreferred;
            }

            int selectedTargetDistance = board.Distance(
                selectedAttackPosition,
                AttackPositionSelector.GetTargetPlanningHex(selected));
            if (candidateTargetDistance != selectedTargetDistance)
            {
                return candidateTargetDistance < selectedTargetDistance;
            }

            if (candidateHp != selected.CurrentHp)
            {
                return candidateHp < selected.CurrentHp;
            }

            if (candidate.UnitId != selected.UnitId)
            {
                return candidate.UnitId < selected.UnitId;
            }

            return CompareHexCoords(candidateAttackPosition, selectedAttackPosition) < 0;
        }

        private static void FillDynamicBlockedHexes(
            BattleSimulation simulation,
            HashSet<HexCoord> occupiedHexes,
            HashSet<HexCoord> dynamicBlockedHexes)
        {
            occupiedHexes.Clear();
            dynamicBlockedHexes.Clear();
            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                occupiedHexes.Add(unit.CurrentHex);
                dynamicBlockedHexes.Add(unit.CurrentHex);
                if (unit.IsMoving)
                {
                    occupiedHexes.Add(unit.MovementDestination);
                    dynamicBlockedHexes.Add(unit.MovementDestination);
                }
            }
        }

        private static void BuildPath(
            HexCoord start,
            HexCoord goal,
            List<HexCoord> path,
            List<HexCoord> reversedPath,
            Dictionary<HexCoord, HexCoord> cameFrom)
        {
            path.Clear();
            reversedPath.Clear();
            HexCoord current = goal;
            while (current != start)
            {
                reversedPath.Add(current);
                current = cameFrom[current];
            }

            reversedPath.Add(start);
            for (int i = reversedPath.Count - 1; i >= 0; i--)
            {
                path.Add(reversedPath[i]);
            }
        }

        private static int CompareHexCoords(HexCoord left, HexCoord right)
        {
            int qCompare = left.Q.CompareTo(right.Q);
            return qCompare != 0 ? qCompare : left.R.CompareTo(right.R);
        }

        private static void ValidateArguments(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            Workspace workspace)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }
        }

        public sealed class Workspace
        {
            internal readonly AttackPositionSelector.Workspace AttackPosition;

            public Workspace(int boardCellCapacity)
            {
                int capacity = Math.Max(1, boardCellCapacity);
                AttackPosition = new AttackPositionSelector.Workspace(capacity);
            }

            internal void Clear()
            {
                AttackPosition.Clear();
            }
        }

        public readonly struct TargetSelection
        {
            public readonly UnitRuntimeState Target;
            public readonly AttackPositionSelector.AttackPathResult AttackPath;

            public TargetSelection(UnitRuntimeState target, AttackPositionSelector.AttackPathResult attackPath)
            {
                Target = target;
                AttackPath = attackPath;
            }

            public bool HasTarget
            {
                get { return Target != null; }
            }
        }
    }
}

namespace DeckBattle
{
    /// <summary>Shared baseline for combat target validity. Future status effects extend this point.</summary>
    public static class TargetingRules
    {
        public static bool CanBeTargeted(UnitRuntimeState attacker, UnitRuntimeState candidate)
        {
            return attacker != null
                && attacker.IsAlive
                && candidate != null
                && candidate.IsAlive
                && attacker.Side != candidate.Side;
        }
    }
}
