using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleController : MonoBehaviour
    {
        private const int MaxAutomaticFlowSteps = 32;

        public event System.Action StateChanged;

        [Header("Config")]
        [SerializeField] private BattleConfig battleConfig;
        [SerializeField] private List<UnitDefinition> playerDeck = new List<UnitDefinition>(8);
        [SerializeField] private List<UnitDefinition> enemyDeck = new List<UnitDefinition>(8);
        [SerializeField] private int seed = 12345;

        [Header("Presentation")]
        [SerializeField] private BoardPresenter boardPresenter;
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private Transform unitRoot;

        [Header("Combat Timing")]
        [SerializeField] private float combatTickDuration = 0.35f;
        [SerializeField] private int maxCombatTicks = CombatSimulator.DefaultMaxTicks;
        [SerializeField] private float roundResolutionDelay = 0.25f;

        private readonly List<UnitView> unitViews = new List<UnitView>(16);
        private readonly Dictionary<int, UnitView> unitViewByRuntimeId = new Dictionary<int, UnitView>(16);
        private readonly BattleEventQueue combatEventQueue = new BattleEventQueue(32);
        private BattleState state;
        private BattleSimulation activeSimulation;
        private BattleTickLoop activeTickLoop;
        private CombatSimulationResult lastCombatResult;
        private RoundResolutionResult lastRoundResolutionResult;
        private Coroutine combatRoutine;
        private bool isCombatAnimating;

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

        public RoundResolutionResult LastRoundResolutionResult
        {
            get { return lastRoundResolutionResult; }
        }

        private void OnValidate()
        {
            combatTickDuration = Mathf.Max(0.05f, combatTickDuration);
            maxCombatTicks = Mathf.Max(1, maxCombatTicks);
            roundResolutionDelay = Mathf.Max(0f, roundResolutionDelay);
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

            StopCombatRoutine();
            ClearUnitViews();
            state = BattleState.Create(battleConfig, playerDeck, enemyDeck, seed);
            lastCombatResult = null;
            lastRoundResolutionResult = null;
            boardPresenter.Build(state.Board);

            ProgressAutomaticFlow();
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
            ProgressAutomaticFlow();
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
            ProgressAutomaticFlow();
            RefreshUnits();
            RaiseStateChanged();
            return true;
        }

        private bool ExecuteEnemyPreparationTurns()
        {
            if (state == null)
            {
                return false;
            }

            bool progressed = false;
            while (state.Phase == BattlePhase.Preparation && state.ActivePreparationSide == BattleSide.Enemy && !state.Enemy.IsReady)
            {
                EnemyPreparationAIResult aiResult = EnemyPreparationAI.ExecuteTurn(state);
                progressed = true;
                if (aiResult.PlayedUnit && aiResult.Unit != null)
                {
                    CreateOrUpdateUnitView(aiResult.Unit);
                }

                if (!state.Player.IsReady || !aiResult.PlayedUnit)
                {
                    break;
                }
            }

            return progressed;
        }

        private bool ResolveCombatAndRoundIfReady()
        {
            if (state == null || state.Phase != BattlePhase.Combat)
            {
                return false;
            }

            if (isCombatAnimating)
            {
                return false;
            }

            if (Application.isPlaying)
            {
                combatRoutine = StartCoroutine(RunCombatRoutine());
                return true;
            }

            lastCombatResult = RunCombatSynchronously();
            if (state.Phase == BattlePhase.RoundResolution)
            {
                lastRoundResolutionResult = RoundFlowService.ResolveRoundAndStartNext(state);
            }

            return true;
        }

        private void ProgressAutomaticFlow()
        {
            if (state == null)
            {
                return;
            }

            for (int step = 0; step < MaxAutomaticFlowSteps; step++)
            {
                if (isCombatAnimating)
                {
                    return;
                }

                if (state.Phase == BattlePhase.MatchEnd)
                {
                    return;
                }

                if (state.Phase == BattlePhase.Preparation && state.ActivePreparationSide == BattleSide.Player && !state.Player.IsReady)
                {
                    return;
                }

                bool progressed = false;
                if (state.Phase == BattlePhase.Preparation && state.ActivePreparationSide == BattleSide.Enemy)
                {
                    progressed = ExecuteEnemyPreparationTurns();
                }
                else if (state.Phase == BattlePhase.Combat)
                {
                    progressed = ResolveCombatAndRoundIfReady();
                }

                if (!progressed)
                {
                    return;
                }
            }

            Debug.LogWarning("Automatic battle flow reached its safety step limit.", this);
        }

        private IEnumerator RunCombatRoutine()
        {
            isCombatAnimating = true;
            lastCombatResult = null;
            lastRoundResolutionResult = null;

            activeSimulation = BattleSimulationFactory.Create(state, BattleRuntimeTuning.Default);
            activeTickLoop = new BattleTickLoop(activeSimulation, combatTickDuration);
            var tickWait = new WaitForSeconds(combatTickDuration);
            var roundWait = new WaitForSeconds(roundResolutionDelay);
            CombatSimulationResult result = null;
            int ticks = 0;

            while (state != null && state.Phase == BattlePhase.Combat && result == null)
            {
                BattleTickResult tickResult = activeTickLoop.Tick(activeSimulation, combatEventQueue);
                ticks++;
                ProcessCombatEvents(combatEventQueue.Events);

                if (tickResult.BattleEnded)
                {
                    state.Phase = BattlePhase.RoundResolution;
                    BattleSimulationResultApplier.Apply(state, activeSimulation);
                    result = BattleSimulationCombatService.CreateCombatResult(tickResult, ticks);
                }
                else if (ticks >= maxCombatTicks)
                {
                    state.Phase = BattlePhase.RoundResolution;
                    BattleSimulationResultApplier.Apply(state, activeSimulation);
                    result = CombatSimulationResult.MaxTicksReached(ticks);
                }

                RaiseStateChanged();
                yield return tickWait;
            }

            if (state != null && state.Phase == BattlePhase.RoundResolution)
            {
                lastCombatResult = result != null ? result : CombatSimulationResult.MaxTicksReached(maxCombatTicks);
                if (roundResolutionDelay > 0f)
                {
                    yield return roundWait;
                }

                lastRoundResolutionResult = RoundFlowService.ResolveRoundAndStartNext(state);
                RefreshUnits();
            }

            isCombatAnimating = false;
            combatRoutine = null;
            activeSimulation = null;
            activeTickLoop = null;
            RaiseStateChanged();
            ProgressAutomaticFlow();
        }

        private CombatSimulationResult RunCombatSynchronously()
        {
            activeSimulation = BattleSimulationFactory.Create(state, BattleRuntimeTuning.Default);
            activeTickLoop = new BattleTickLoop(activeSimulation, combatTickDuration);
            CombatSimulationResult result = BattleSimulationCombatService.RunToResolution(
                state,
                activeSimulation,
                activeTickLoop,
                maxCombatTicks,
                combatEventQueue);
            activeSimulation = null;
            activeTickLoop = null;
            return result;
        }

        private void ProcessCombatEvents(IReadOnlyList<BattleEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                switch (battleEvent.Type)
                {
                    case BattleEventType.UnitMoved:
                        HandleCombatUnitMoved(battleEvent);
                        break;
                    case BattleEventType.UnitAttackStarted:
                        HandleCombatUnitAttackStarted(battleEvent);
                        break;
                    case BattleEventType.UnitDamaged:
                        HandleCombatUnitDamaged(battleEvent);
                        break;
                    case BattleEventType.UnitDied:
                        HandleCombatUnitDied(battleEvent);
                        break;
                }
            }
        }

        private void HandleCombatUnitMoved(BattleEvent battleEvent)
        {
            UnitView view;
            if (!unitViewByRuntimeId.TryGetValue(battleEvent.UnitId, out view) || view == null)
            {
                return;
            }

            view.MoveToWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To), combatTickDuration);
        }

        private void HandleCombatUnitAttackStarted(BattleEvent battleEvent)
        {
            UnitView view;
            if (unitViewByRuntimeId.TryGetValue(battleEvent.UnitId, out view) && view != null)
            {
                view.PlayAttack();
            }
        }

        private void HandleCombatUnitDamaged(BattleEvent battleEvent)
        {
            UnitView view;
            if (unitViewByRuntimeId.TryGetValue(battleEvent.UnitId, out view) && view != null)
            {
                view.PlayDamage(battleEvent.RemainingHp);
            }
        }

        private void HandleCombatUnitDied(BattleEvent battleEvent)
        {
            UnitView view;
            if (unitViewByRuntimeId.TryGetValue(battleEvent.UnitId, out view) && view != null)
            {
                view.PlayDeath();
            }
        }

        private void StopCombatRoutine()
        {
            if (combatRoutine != null)
            {
                StopCoroutine(combatRoutine);
                combatRoutine = null;
            }

            isCombatAnimating = false;
            combatEventQueue.Clear();
            activeSimulation = null;
            activeTickLoop = null;
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
