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
        [SerializeField] private bool showUnitHexLabels = true;

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
        [SerializeField] private Color unitHexLabelColor = new Color(1f, 1f, 1f, 1f);

        private readonly List<HexCoord> rangeHexes = new List<HexCoord>(64);
        private readonly Dictionary<int, HexCoord> plannedMovementDestinations = new Dictionary<int, HexCoord>(16);

        private TargetSelector.Workspace targetWorkspace;
        private AttackPositionSelector.Workspace attackPositionWorkspace;
        private MovementResolver.Workspace movementWorkspace;
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

            if (showPaths)
            {
                MovementResolver.PlanMovementDestinations(simulation, movementWorkspace, plannedMovementDestinations);
            }
            else
            {
                plannedMovementDestinations.Clear();
            }

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (showLabels && showUnitHexLabels)
                {
                    DrawUnitHexLabel(presenter, unit);
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
                    DrawActualMovementStep(simulation, presenter, unit, target);
                }
            }
        }

        private void DrawUnitHexLabel(BoardPresenter presenter, UnitRuntimeState unit)
        {
            Vector3 position = presenter.GetWorldPosition(unit.CurrentHex) + Vector3.up * (lineHeight + 0.25f);
            string label = "U" + unit.UnitId + " hex " + unit.CurrentHex;
            if (unit.IsMoving)
            {
                label += " -> " + unit.MovementDestination;
            }

            DrawLabel(position, label, unitHexLabelColor);
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
            movementWorkspace = new MovementResolver.Workspace(capacity, capacity);
            rangeHexes.Capacity = Mathf.Max(rangeHexes.Capacity, capacity);
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

        private void DrawActualMovementStep(
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

            HexCoord destination;
            if (unit.IsMoving)
            {
                destination = unit.MovementDestination;
            }
            else if (!plannedMovementDestinations.TryGetValue(unit.UnitId, out destination))
            {
                return;
            }

            Gizmos.color = pathColor;
            Vector3 from = presenter.GetWorldPosition(unit.CurrentHex) + Vector3.up * pathHeight;
            Vector3 to = presenter.GetWorldPosition(destination) + Vector3.up * pathHeight;
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(to, markerRadius * 0.85f);
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
