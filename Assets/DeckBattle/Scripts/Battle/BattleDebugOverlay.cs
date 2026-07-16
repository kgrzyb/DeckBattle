using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeckBattle
{
    public sealed class BattleDebugOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleView battleView;
        [SerializeField] private BoardPresenter boardPresenter;

        [Header("Visibility")]
        [SerializeField] private bool playModeOnly = true;
        [SerializeField] private bool showTargets = true;
        [SerializeField] private bool showPaths = true;
        [SerializeField] private bool showRanges = true;
        [SerializeField] private bool showOccupiedHexes = true;
        [SerializeField] private bool showReservedHexes = true;
        [SerializeField] private bool showLabels = true;

        [Header("Style")]
        [SerializeField] private float markerRadius = 0.18f;
        [SerializeField] private float pathHeight = 0.2f;
        [SerializeField] private float lineHeight = 0.55f;
        [SerializeField] private Color targetColor = new Color(1f, 0.82f, 0.16f, 1f);
        [SerializeField] private Color pathColor = new Color(0.25f, 0.9f, 1f, 1f);
        [SerializeField] private Color rangeColor = new Color(0.1f, 0.9f, 0.35f, 0.55f);
        [SerializeField] private Color occupiedColor = new Color(1f, 0.1f, 0.1f, 0.85f);
        [SerializeField] private Color reservedColor = new Color(0.55f, 0.2f, 1f, 0.9f);
        [SerializeField] private Color attackPositionColor = new Color(1f, 1f, 1f, 0.95f);

        private readonly List<HexCoord> rangeHexes = new List<HexCoord>(64);
        private readonly List<HexCoord> path = new List<HexCoord>(64);

        private TargetSelector.Workspace targetWorkspace;
        private AttackPositionSelector.Workspace attackPositionWorkspace;
        private HexBoard.PathfindingWorkspace pathfindingWorkspace;
        private int workspaceCapacity;

        private void OnValidate()
        {
            markerRadius = Mathf.Max(0.02f, markerRadius);
            pathHeight = Mathf.Max(0f, pathHeight);
            lineHeight = Mathf.Max(0f, lineHeight);
        }

        private void OnDrawGizmos()
        {
            if (playModeOnly && !Application.isPlaying)
            {
                return;
            }

            BattleSimulation simulation = ResolveSimulation();
            if (simulation == null)
            {
                return;
            }

            BoardPresenter presenter = ResolveBoardPresenter();
            if (presenter == null)
            {
                return;
            }

            EnsureWorkspace(simulation.Board.Width * simulation.Board.Height);

            if (showOccupiedHexes)
            {
                DrawHexDictionary(simulation, presenter, battleView.DebugSnapshot.OccupiedHexes, occupiedColor, "O");
            }

            if (showReservedHexes)
            {
                DrawHexDictionary(simulation, presenter, battleView.DebugSnapshot.ReservedHexes, reservedColor, "R");
            }

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                UnitRuntimeState target = ResolveTarget(simulation, unit);
                if (showRanges)
                {
                    DrawAttackRange(simulation, presenter, unit);
                }

                if (target == null)
                {
                    if (showLabels)
                    {
                        DrawLabel(presenter.GetWorldPosition(unit.CurrentHex) + Vector3.up * lineHeight, "U" + unit.UnitId + " no target", targetColor);
                    }

                    continue;
                }

                if (showTargets)
                {
                    DrawTargetLine(presenter, unit, target);
                }

                if (showPaths)
                {
                    DrawPathToAttackPosition(simulation, presenter, unit, target);
                }
            }
        }

        private BattleSimulation ResolveSimulation()
        {
            if (battleView == null)
            {
                battleView = GetComponent<BattleView>();
            }

            return battleView != null ? battleView.Simulation : null;
        }

        private BoardPresenter ResolveBoardPresenter()
        {
            if (boardPresenter == null && battleView != null)
            {
                boardPresenter = battleView.BoardPresenter;
            }

            return boardPresenter;
        }

        private void EnsureWorkspace(int capacity)
        {
            if (targetWorkspace != null && workspaceCapacity == capacity)
            {
                return;
            }

            workspaceCapacity = capacity;
            targetWorkspace = new TargetSelector.Workspace(capacity);
            attackPositionWorkspace = new AttackPositionSelector.Workspace(capacity);
            pathfindingWorkspace = new HexBoard.PathfindingWorkspace(capacity);
            rangeHexes.Capacity = Mathf.Max(rangeHexes.Capacity, capacity);
            path.Capacity = Mathf.Max(path.Capacity, capacity);
        }

        private UnitRuntimeState ResolveTarget(BattleSimulation simulation, UnitRuntimeState unit)
        {
            UnitRuntimeState target;
            if (unit.TargetUnitId != UnitRuntimeState.NoTargetUnitId
                && simulation.TryGetUnitById(unit.TargetUnitId, out target)
                && target != null
                && target.IsAlive)
            {
                return target;
            }

            return TargetSelector.SelectTarget(simulation, unit, targetWorkspace);
        }

        private void DrawAttackRange(BattleSimulation simulation, BoardPresenter presenter, UnitRuntimeState unit)
        {
            rangeHexes.Clear();
            simulation.Board.FillHexesInRange(unit.CurrentHex, simulation.Tuning.GetAttackRange(unit.Definition), rangeHexes);
            Gizmos.color = rangeColor;
            for (int i = 0; i < rangeHexes.Count; i++)
            {
                Vector3 position = presenter.GetWorldPosition(rangeHexes[i]) + Vector3.up * pathHeight;
                Gizmos.DrawWireSphere(position, markerRadius);
            }
        }

        private void DrawTargetLine(BoardPresenter presenter, UnitRuntimeState unit, UnitRuntimeState target)
        {
            Gizmos.color = targetColor;
            Vector3 from = presenter.GetWorldPosition(unit.CurrentHex) + Vector3.up * lineHeight;
            Vector3 to = presenter.GetWorldPosition(target.CurrentHex) + Vector3.up * lineHeight;
            Gizmos.DrawLine(from, to);
            if (showLabels)
            {
                DrawLabel((from + to) * 0.5f, "U" + unit.UnitId + " -> U" + target.UnitId, targetColor);
            }
        }

        private void DrawPathToAttackPosition(
            BattleSimulation simulation,
            BoardPresenter presenter,
            UnitRuntimeState unit,
            UnitRuntimeState target)
        {
            HexCoord attackPosition;
            if (!AttackPositionSelector.TrySelectAttackPosition(simulation, unit, target, attackPositionWorkspace, out attackPosition))
            {
                return;
            }

            Gizmos.color = attackPositionColor;
            Gizmos.DrawWireSphere(presenter.GetWorldPosition(attackPosition) + Vector3.up * pathHeight, markerRadius * 1.35f);

            path.Clear();
            if (!simulation.Board.TryFindPath(unit.CurrentHex, attackPosition, path, pathfindingWorkspace) || path.Count < 2)
            {
                return;
            }

            Gizmos.color = pathColor;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = presenter.GetWorldPosition(path[i - 1]) + Vector3.up * pathHeight;
                Vector3 to = presenter.GetWorldPosition(path[i]) + Vector3.up * pathHeight;
                Gizmos.DrawLine(from, to);
            }
        }

        private void DrawHexDictionary(
            BattleSimulation simulation,
            BoardPresenter presenter,
            IReadOnlyDictionary<HexCoord, int> hexes,
            Color color,
            string labelPrefix)
        {
            if (hexes == null)
            {
                return;
            }

            Gizmos.color = color;
            foreach (KeyValuePair<HexCoord, int> entry in hexes)
            {
                if (!simulation.Board.IsValidHex(entry.Key))
                {
                    continue;
                }

                Vector3 position = presenter.GetWorldPosition(entry.Key) + Vector3.up * pathHeight;
                Gizmos.DrawWireCube(position, Vector3.one * markerRadius);
                if (showLabels)
                {
                    DrawLabel(position + Vector3.up * 0.08f, labelPrefix + entry.Value, color);
                }
            }
        }

        private static void DrawLabel(Vector3 position, string text, Color color)
        {
#if UNITY_EDITOR
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            Handles.Label(position, text, style);
#endif
        }
    }
}
