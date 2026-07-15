using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleController : MonoBehaviour
    {
        public event System.Action StateChanged;

        [Header("Config")]
        [SerializeField] private BattleConfig battleConfig;
        [SerializeField] private List<UnitDefinition> playerDeck = new List<UnitDefinition>(8);
        [SerializeField] private List<UnitDefinition> enemyDeck = new List<UnitDefinition>(8);
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool playInitialVisibleUnits;

        [Header("Presentation")]
        [SerializeField] private BoardPresenter boardPresenter;
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private Transform unitRoot;

        private readonly List<UnitView> unitViews = new List<UnitView>(16);
        private readonly Dictionary<int, UnitView> unitViewByRuntimeId = new Dictionary<int, UnitView>(16);
        private BattleState state;
        private CombatSimulationResult lastCombatResult;

        public BattleState State
        {
            get { return state; }
        }

        public BoardPresenter BoardPresenter
        {
            get { return boardPresenter; }
        }

        public CombatSimulationResult LastCombatResult
        {
            get { return lastCombatResult; }
        }

        private void Start()
        {
            StartTestBattle();
        }

        public void StartTestBattle()
        {
            if (battleConfig == null || boardPresenter == null || unitPrefab == null)
            {
                Debug.LogError("BattleController is missing required references.", this);
                return;
            }

            state = BattleState.Create(battleConfig, playerDeck, enemyDeck, seed);
            lastCombatResult = null;
            boardPresenter.Build(state.Board);
            if (playInitialVisibleUnits)
            {
                PlayInitialVisibleUnits();
            }

            ExecuteEnemyPreparationTurns();
            ResolveCombatIfReady();
            RefreshUnits();
            RaiseStateChanged();
        }

        public bool TryPlayPlayerCard(CardRuntimeState card, HexCoord coord)
        {
            if (state == null || state.Phase != BattlePhase.Preparation || state.ActivePreparationSide != BattleSide.Player || state.Player.IsReady)
            {
                return false;
            }

            PlayUnitResult result = UnitPlayService.PlayUnit(state, state.Player, card, coord);
            if (!result.Success)
            {
                return false;
            }

            CreateOrUpdateUnitView(result.Unit);
            PreparationTurnService.CompleteActiveSideAction(state);
            ExecuteEnemyPreparationTurns();
            ResolveCombatIfReady();
            RefreshUnits();
            RaiseStateChanged();
            return true;
        }

        public bool TryMovePlayerUnit(RuntimeUnit unit, HexCoord coord)
        {
            if (state == null || state.Phase != BattlePhase.Preparation || state.ActivePreparationSide != BattleSide.Player || state.Player.IsReady)
            {
                return false;
            }

            FormationMoveResult result = FormationService.MoveUnit(state, state.Player, unit, coord);
            if (!result.Success)
            {
                return false;
            }

            UpdateUnitView(unit);
            RaiseStateChanged();
            return true;
        }

        public bool ConfirmReady()
        {
            if (state == null || state.Phase != BattlePhase.Preparation || state.ActivePreparationSide != BattleSide.Player || state.Player.IsReady)
            {
                return false;
            }

            PreparationTurnService.MarkActiveSideReadyAndAdvance(state);
            ExecuteEnemyPreparationTurns();
            ResolveCombatIfReady();
            RefreshUnits();
            RaiseStateChanged();
            return true;
        }

        private void ExecuteEnemyPreparationTurns()
        {
            if (state == null)
            {
                return;
            }

            while (state.Phase == BattlePhase.Preparation && state.ActivePreparationSide == BattleSide.Enemy && !state.Enemy.IsReady)
            {
                EnemyPreparationAIResult aiResult = EnemyPreparationAI.ExecuteTurn(state);
                if (aiResult.PlayedUnit && aiResult.Unit != null)
                {
                    CreateOrUpdateUnitView(aiResult.Unit);
                }

                if (!state.Player.IsReady || !aiResult.PlayedUnit)
                {
                    break;
                }
            }
        }

        private void ResolveCombatIfReady()
        {
            if (state == null || state.Phase != BattlePhase.Combat)
            {
                return;
            }

            lastCombatResult = CombatSimulator.Simulate(state);
        }

        private void PlayInitialVisibleUnits()
        {
            TryPlayFirstAvailable(state.Player, new HexCoord(1, 0));
            TryPlayFirstAvailable(state.Player, new HexCoord(3, 0));
            TryPlayFirstAvailable(state.Enemy, new HexCoord(1, battleConfig.BoardHeight - 1));
            TryPlayFirstAvailable(state.Enemy, new HexCoord(3, battleConfig.BoardHeight - 1));
        }

        private void TryPlayFirstAvailable(PlayerBattleState player, HexCoord coord)
        {
            if (player.Hand.Count == 0)
            {
                return;
            }

            for (int i = 0; i < player.Hand.Count; i++)
            {
                CardRuntimeState card = player.Hand[i];
                if (UnitPlayService.ValidatePlay(state, player, card, coord) != PlayUnitFailReason.None)
                {
                    continue;
                }

                UnitPlayService.PlayUnit(state, player, card, coord);
                return;
            }
        }

        private void RefreshUnits()
        {
            if (state == null)
            {
                ClearUnitViews();
                return;
            }

            SyncUnitViews(state.Player.Units);
            SyncUnitViews(state.Enemy.Units);
        }

        private void SyncUnitViews(List<RuntimeUnit> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                CreateOrUpdateUnitView(units[i]);
            }
        }

        private void CreateOrUpdateUnitView(RuntimeUnit unit)
        {
            UnitView view;
            if (unitViewByRuntimeId.TryGetValue(unit.RuntimeId, out view) && view != null)
            {
                view.Bind(unit, boardPresenter.GetWorldPosition(unit.BattleCoord));
                return;
            }

            Transform parent = unitRoot != null ? unitRoot : transform;
            view = Instantiate(unitPrefab, parent);
            view.Bind(unit, boardPresenter.GetWorldPosition(unit.BattleCoord));
            unitViews.Add(view);
            unitViewByRuntimeId.Add(unit.RuntimeId, view);
        }

        private void UpdateUnitView(RuntimeUnit unit)
        {
            UnitView view;
            if (!unitViewByRuntimeId.TryGetValue(unit.RuntimeId, out view) || view == null)
            {
                CreateOrUpdateUnitView(unit);
                return;
            }

            view.SetWorldPosition(boardPresenter.GetWorldPosition(unit.BattleCoord));
        }

        private void ClearUnitViews()
        {
            for (int i = unitViews.Count - 1; i >= 0; i--)
            {
                if (unitViews[i] != null)
                {
                    Destroy(unitViews[i].gameObject);
                }
            }

            unitViews.Clear();
            unitViewByRuntimeId.Clear();
        }

        private void RaiseStateChanged()
        {
            if (StateChanged != null)
            {
                StateChanged.Invoke();
            }
        }
    }
}
