using System;
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
        [SerializeField] private BattleTimingConfig battleTimingConfig;
        [SerializeField] private List<CardDefinition> playerDeck = new List<CardDefinition>(8);
        [SerializeField] private List<CardDefinition> enemyDeck = new List<CardDefinition>(8);
        [SerializeField] private bool randomizeSeedOnPlay;
        [SerializeField] private int seed = 12345;

        [Header("Presentation")]
        [SerializeField] private BoardPresenter boardPresenter;
        [SerializeField] private Transform unitRoot;
        [SerializeField] private BattleView battleView;
        [SerializeField] private UnitStatusOverlayController statusOverlayController;
        [SerializeField] private RoundAnnouncementView roundAnnouncementView;

        [Header("Combat Timing")]
        [SerializeField, HideInInspector] private float combatTickDuration = BattleTiming.DefaultCombatTickDuration;
        [SerializeField, HideInInspector] private int maxCombatTicks = BattleTiming.DefaultMaxCombatTicks;
        [SerializeField, HideInInspector] private float roundResolutionDelay = BattleTiming.DefaultRoundResolutionDelay;

        private readonly List<UnitView> unitViews = new List<UnitView>(16);
        private readonly List<UnitView> unitViewSearchBuffer = new List<UnitView>(16);
        private readonly Dictionary<int, UnitView> unitViewByRuntimeId = new Dictionary<int, UnitView>(16);
        private BattleState state;
        private BattleSimulation activeSimulation;
        private CombatSimulationResult lastCombatResult;
        private RoundResolutionResult lastRoundResolutionResult;
        private int activeSeed;
        private Coroutine combatRoutine;
        private Coroutine roundAnnouncementRoutine;
        private Coroutine preparationCountdownRoutine;
        private bool isCombatAnimating;
        private bool isRoundAnnouncementAnimating;

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

        public int ActiveSeed
        {
            get { return activeSeed; }
        }

        private void OnValidate()
        {
            combatTickDuration = Mathf.Max(BattleTiming.MinCombatTickDuration, combatTickDuration);
            maxCombatTicks = Mathf.Max(1, maxCombatTicks);
            roundResolutionDelay = Mathf.Max(0f, roundResolutionDelay);
        }

        private void Awake()
        {
            ResolveBattleView();
        }

        private void Start()
        {
            StartTestBattle();
        }

        public void StartTestBattle()
        {
            if (battleConfig == null || boardPresenter == null)
            {
                Debug.LogError("BattleController is missing required references.", this);
                return;
            }

            StopCombatRoutine();
            StopRoundAnnouncementRoutine();
            StopPreparationCountdownRoutine();
            ClearUnitViews();
            BattleStartData startData;
            bool hasPendingStartData = BattleSession.TryConsumePendingStartData(out startData);
            if (hasPendingStartData && IsUsableStartData(startData))
            {
                activeSeed = startData.Seed;
                state = BattleState.Create(battleConfig, startData.PlayerDeck, startData.EnemyDeck, activeSeed);
            }
            else
            {
                if (hasPendingStartData)
                {
                    Debug.LogWarning("BattleSession start data is incomplete. Falling back to BattleController inspector decks.", this);
                }

                activeSeed = ResolveBattleSeed();
                state = BattleState.Create(battleConfig, playerDeck, enemyDeck, activeSeed);
            }

            state.BeginRoundStart();
            lastCombatResult = null;
            lastRoundResolutionResult = null;
            boardPresenter.Build(state.Board);

            ProgressAutomaticFlow();
            RefreshUnits();
            RaiseStateChanged();
        }

        private static bool IsUsableStartData(BattleStartData startData)
        {
            return startData != null
                && startData.PlayerDeck != null
                && startData.PlayerDeck.Count > 0
                && startData.EnemyDeck != null
                && startData.EnemyDeck.Count > 0;
        }

        private int ResolveBattleSeed()
        {
            return randomizeSeedOnPlay && Application.isPlaying ? GeneratePlaySeed() : seed;
        }

        private int GeneratePlaySeed()
        {
            unchecked
            {
                long ticks = DateTime.UtcNow.Ticks;
                int generatedSeed = (int)ticks;
                generatedSeed = (generatedSeed * 397) ^ (int)(ticks >> 32);
                generatedSeed = (generatedSeed * 397) ^ GetInstanceID();
                generatedSeed = (generatedSeed * 397) ^ Time.frameCount;
                return generatedSeed;
            }
        }

        public bool TryPlayPlayerCard(CardRuntimeState card, HexCoord coord)
        {
            if (!PreparationTurnService.CanPlayerPrepare(state))
            {
                return false;
            }

            if (card == null || card.Definition == null)
            {
                return false;
            }

            if (card.Definition.CardKind != CardKind.Unit || card.UnitDefinition == null)
            {
                return false;
            }

            PlayUnitResult result = UnitPlayService.PlayUnit(state, state.Player, card, coord);
            if (!result.Success)
            {
                return false;
            }

            CreateOrUpdateUnitView(result.Unit);
            EvaluatePreparationCountdownState();
            ProgressAutomaticFlow();
            RefreshUnits();
            RaiseStateChanged();
            return true;
        }

        public bool TryPlayPlayerSpell(CardRuntimeState card, SpellTarget target)
        {
            if (!PreparationTurnService.CanPlayerPrepare(state))
            {
                return false;
            }

            if (card == null || card.SpellDefinition == null)
            {
                return false;
            }

            PlaySpellResult result = SpellPlayService.PlaySpell(state, state.Player, card, target);
            if (!result.Success)
            {
                return false;
            }

            EvaluatePreparationCountdownState();
            ProgressAutomaticFlow();
            RefreshUnits();
            RaiseStateChanged();
            return true;
        }

        public bool TryMovePlayerUnit(RuntimeUnit unit, HexCoord coord)
        {
            if (!PreparationTurnService.CanPlayerPrepare(state))
            {
                return false;
            }

            FormationMoveResult result = FormationService.MoveUnit(state, state.Player, unit, coord);
            if (!result.Success)
            {
                return false;
            }

            UpdateUnitView(unit);
            if (result.SwappedUnit != null)
            {
                UpdateUnitView(result.SwappedUnit);
            }

            RaiseStateChanged();
            return true;
        }

        public bool ConfirmReady()
        {
            if (!PreparationTurnService.CanPlayerPrepare(state))
            {
                return false;
            }

            PreparationTurnService.MarkPlayerReady(state);
            EvaluatePreparationCountdownState();
            ProgressAutomaticFlow();
            RefreshUnits();
            RaiseStateChanged();
            return true;
        }

        private bool ExecuteEnemyPreparation()
        {
            if (!PreparationTurnService.CanEnemyPrepare(state))
            {
                return false;
            }

            EnemyPreparationAIResult aiResult = EnemyPreparationAI.PrepareFormation(state);
            return aiResult.PlayedUnit || aiResult.MarkedReady;
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

            EnsureCombatUnitViews();

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
                EvaluatePreparationCountdownState();

                if (isCombatAnimating || isRoundAnnouncementAnimating)
                {
                    return;
                }

                if (state.Phase == BattlePhase.MatchEnd)
                {
                    return;
                }

                if (state.PreparationCountdownActive)
                {
                    return;
                }

                bool progressed = false;
                if (state.Phase == BattlePhase.RoundStart)
                {
                    progressed = BeginRoundStartPresentation();
                }

                if (state.Phase == BattlePhase.Preparation)
                {
                    if (!state.Enemy.IsReady)
                    {
                        progressed = ExecuteEnemyPreparation();
                        EvaluatePreparationCountdownState();
                    }

                    if (state.Phase == BattlePhase.Preparation && !state.Player.IsReady)
                    {
                        return;
                    }
                }

                if (!progressed && state.Phase == BattlePhase.Combat)
                {
                    progressed = ResolveCombatAndRoundIfReady();
                }

                if (!progressed)
                {
                    EvaluatePreparationCountdownState();
                    return;
                }
            }

            Debug.LogWarning("Automatic battle flow reached its safety step limit.", this);
        }

        private bool BeginRoundStartPresentation()
        {
            if (state == null || state.Phase != BattlePhase.RoundStart)
            {
                return false;
            }

            if (Application.isPlaying)
            {
                roundAnnouncementRoutine = StartCoroutine(RunRoundStartPresentationRoutine());
                return true;
            }

            state.BeginPreparationAfterRoundStart();
            RefreshUnits();
            RaiseStateChanged();
            return true;
        }

        private IEnumerator RunRoundStartPresentationRoutine()
        {
            isRoundAnnouncementAnimating = true;

            if (roundAnnouncementView != null && state != null)
            {
                yield return roundAnnouncementView.PlayRoundStart(state.RoundNumber);
            }

            if (state != null && state.Phase == BattlePhase.RoundStart)
            {
                state.BeginPreparationAfterRoundStart();
                RefreshUnits();
                RaiseStateChanged();
            }

            isRoundAnnouncementAnimating = false;
            roundAnnouncementRoutine = null;
            ProgressAutomaticFlow();
        }

        private IEnumerator RunCombatRoutine()
        {
            isCombatAnimating = true;
            lastCombatResult = null;
            lastRoundResolutionResult = null;

            BattleView resolvedBattleView = ResolveBattleView();
            if (resolvedBattleView == null)
            {
                Debug.LogError("BattleController requires BattleView for realtime combat presentation.", this);
                lastCombatResult = RunCombatSynchronously();
                yield return FinishRoundAfterCombat(null);
                FinishCombatRoutine();
                yield break;
            }

            activeSimulation = BattleSimulationFactory.Create(state, BattleRuntimeTuning.Default);
            if (statusOverlayController != null)
            {
                statusOverlayController.ReleaseAll();
            }

            resolvedBattleView.BindSimulation(activeSimulation, ResolveCombatTickDuration(), ResolveMaxCombatTicks(), unitViewByRuntimeId);
            ReleaseUnitViewOwnership();

            while (state != null
                && state.Phase == BattlePhase.Combat
                && activeSimulation != null
                && !activeSimulation.IsBattleEnded
                && !resolvedBattleView.MaxTicksReached)
            {
                yield return null;
            }

            if (state != null && state.Phase == BattlePhase.Combat && activeSimulation != null)
            {
                state.Phase = BattlePhase.RoundResolution;
                BattleSimulationResultApplier.Apply(state, activeSimulation);
                lastCombatResult = activeSimulation.IsBattleEnded
                    ? BattleSimulationCombatService.CreateCombatResult(resolvedBattleView.LastTickResult, resolvedBattleView.TicksElapsed)
                    : CombatSimulationResult.MaxTicksReached(resolvedBattleView.TicksElapsed);
                RaiseStateChanged();
            }

            yield return FinishRoundAfterCombat(resolvedBattleView);
            resolvedBattleView.ClearBattle(false);
            FinishCombatRoutine();
        }

        private IEnumerator FinishRoundAfterCombat(BattleView combatView)
        {
            if (state != null && state.Phase == BattlePhase.RoundResolution)
            {
                float delay = ResolveRoundResolutionDelay();
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }

                lastRoundResolutionResult = RoundFlowService.ResolveRoundAndStartNext(state);
                ReclaimUnitViews(combatView);
                RefreshUnits();
                RaiseStateChanged();

                if (roundAnnouncementView != null)
                {
                    yield return roundAnnouncementView.PlayRoundResult(lastRoundResolutionResult);
                }
            }
        }

        private CombatSimulationResult RunCombatSynchronously()
        {
            activeSimulation = BattleSimulationFactory.Create(state, BattleRuntimeTuning.Default);
            var activeTickLoop = new BattleTickLoop(activeSimulation, ResolveCombatTickDuration());
            var eventQueue = new BattleEventQueue(32);
            CombatSimulationResult result = BattleSimulationCombatService.RunToResolution(
                state,
                activeSimulation,
                activeTickLoop,
                ResolveMaxCombatTicks(),
                eventQueue);
            activeSimulation = null;
            return result;
        }

        private void FinishCombatRoutine()
        {
            isCombatAnimating = false;
            combatRoutine = null;
            activeSimulation = null;
            RaiseStateChanged();
            ProgressAutomaticFlow();
        }

        private void StopCombatRoutine()
        {
            if (combatRoutine != null)
            {
                StopCoroutine(combatRoutine);
                combatRoutine = null;
            }

            BattleView resolvedBattleView = ResolveBattleView();
            if (resolvedBattleView != null)
            {
                resolvedBattleView.ClearBattle();
            }

            isCombatAnimating = false;
            activeSimulation = null;
        }

        private void StopRoundAnnouncementRoutine()
        {
            if (roundAnnouncementRoutine != null)
            {
                StopCoroutine(roundAnnouncementRoutine);
                roundAnnouncementRoutine = null;
            }

            isRoundAnnouncementAnimating = false;

            if (roundAnnouncementView != null)
            {
                roundAnnouncementView.HideImmediate();
            }
        }

        private BattleView ResolveBattleView()
        {
            if (battleView == null)
            {
                battleView = GetComponent<BattleView>();
            }

            return battleView;
        }

        private float ResolveCombatTickDuration()
        {
            float configuredDuration = battleTimingConfig != null
                ? battleTimingConfig.CombatTickDuration
                : combatTickDuration;
            return Mathf.Max(BattleTiming.MinCombatTickDuration, configuredDuration);
        }

        private int ResolveMaxCombatTicks()
        {
            int configuredTicks = battleTimingConfig != null
                ? battleTimingConfig.MaxCombatTicks
                : maxCombatTicks;
            return Mathf.Max(1, configuredTicks);
        }

        private float ResolveRoundResolutionDelay()
        {
            float configuredDelay = battleTimingConfig != null
                ? battleTimingConfig.RoundResolutionDelay
                : roundResolutionDelay;
            return Mathf.Max(0f, configuredDelay);
        }

        private void EvaluatePreparationCountdownState()
        {
            if (state == null || state.Phase != BattlePhase.Preparation)
            {
                StopPreparationCountdownRoutine();
                return;
            }

            if (state.Player.IsReady && state.Enemy.IsReady)
            {
                StopPreparationCountdownRoutine();
                PreparationTurnService.TryStartCombatIfReady(state);
                return;
            }

            if (!PreparationTurnService.ShouldStartPreparationCountdown(state))
            {
                return;
            }

            float duration = state.Config != null ? state.Config.PreparationCountdownSeconds : 10f;
            state.StartPreparationCountdown(duration);
            if (state.PreparationCountdownActive && Application.isPlaying)
            {
                if (preparationCountdownRoutine == null)
                {
                    preparationCountdownRoutine = StartCoroutine(RunPreparationCountdownRoutine());
                }
            }
            else
            {
                CompletePreparationCountdown();
            }
        }

        private IEnumerator RunPreparationCountdownRoutine()
        {
            while (state != null && state.Phase == BattlePhase.Preparation && state.PreparationCountdownActive)
            {
                int previousSeconds = Mathf.CeilToInt(state.PreparationCountdownRemaining);
                if (state.TickPreparationCountdown(Time.deltaTime))
                {
                    preparationCountdownRoutine = null;
                    CompletePreparationCountdown();
                    yield break;
                }

                int currentSeconds = Mathf.CeilToInt(state.PreparationCountdownRemaining);
                if (currentSeconds != previousSeconds)
                {
                    RaiseStateChanged();
                }

                yield return null;
            }

            preparationCountdownRoutine = null;
        }

        private void CompletePreparationCountdown()
        {
            StopPreparationCountdownRoutine();
            if (state == null)
            {
                return;
            }

            state.CompletePreparationCountdown();
            RefreshUnits();
            RaiseStateChanged();
            ProgressAutomaticFlow();
        }

        private void StopPreparationCountdownRoutine()
        {
            if (preparationCountdownRoutine == null)
            {
                return;
            }

            StopCoroutine(preparationCountdownRoutine);
            preparationCountdownRoutine = null;
        }

        private void RefreshUnits()
        {
            if (state == null)
            {
                ClearUnitViews();
                return;
            }

            SyncUnitViews(state.Player.Units);
            if (state.Phase == BattlePhase.RoundStart || state.Phase == BattlePhase.Preparation)
            {
                HideEnemyUnitViews();
            }
            else
            {
                SyncUnitViews(state.Enemy.Units);
            }
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
                BindStatusOverlay(unit, view);
                return;
            }

            if (!TryFindExistingUnitView(unit.RuntimeId, out view))
            {
                view = CreateUnitView(unit.Definition);
                if (view == null)
                {
                    return;
                }
            }

            RemoveDuplicateUnitViews(unit.RuntimeId, view);
            view.Bind(unit, boardPresenter.GetWorldPosition(unit.BattleCoord));
            TrackUnitView(unit.RuntimeId, view);
            BindStatusOverlay(unit, view);
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
            BindStatusOverlay(unit, view);
        }

        private void EnsureCombatUnitViews()
        {
            if (state == null || state.Phase != BattlePhase.Combat)
            {
                return;
            }

            SyncUnitViews(state.Player.Units);
            SyncUnitViews(state.Enemy.Units);
        }

        private void HideEnemyUnitViews()
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0; i < state.Enemy.Units.Count; i++)
            {
                RuntimeUnit unit = state.Enemy.Units[i];
                if (unit == null)
                {
                    continue;
                }

                UnitView view;
                if (!unitViewByRuntimeId.TryGetValue(unit.RuntimeId, out view))
                {
                    continue;
                }

                unitViewByRuntimeId.Remove(unit.RuntimeId);
                if (view != null)
                {
                    unitViews.Remove(view);
                    if (statusOverlayController != null)
                    {
                        statusOverlayController.Release(unit.RuntimeId);
                    }

                    view.gameObject.SetActive(false);
                    Destroy(view.gameObject);
                }
            }
        }

        private UnitView CreateUnitView(UnitDefinition definition)
        {
            if (definition == null || definition.UnitPrefab == null)
            {
                Debug.LogError("Runtime unit definition is missing UnitPrefab.", this);
                return null;
            }

            Transform parent = unitRoot != null ? unitRoot : transform;
            return Instantiate(definition.UnitPrefab, parent);
        }

        private void ClearUnitViews()
        {
            if (statusOverlayController != null)
            {
                statusOverlayController.ReleaseAll();
            }

            for (int i = unitViews.Count - 1; i >= 0; i--)
            {
                if (unitViews[i] != null)
                {
                    unitViews[i].gameObject.SetActive(false);
                    Destroy(unitViews[i].gameObject);
                }
            }

            unitViews.Clear();
            unitViewByRuntimeId.Clear();
        }

        private void ReleaseUnitViewOwnership()
        {
            unitViews.Clear();
            unitViewByRuntimeId.Clear();
        }

        private void ReclaimUnitViews(BattleView combatView)
        {
            if (combatView == null || state == null)
            {
                return;
            }

            ReclaimUnitViews(state.Player.Units, combatView);
            ReclaimUnitViews(state.Enemy.Units, combatView);
        }

        private void ReclaimUnitViews(List<RuntimeUnit> units, BattleView combatView)
        {
            for (int i = 0; i < units.Count; i++)
            {
                RuntimeUnit unit = units[i];
                if (unit == null || unitViewByRuntimeId.ContainsKey(unit.RuntimeId))
                {
                    continue;
                }

                UnitView view = combatView.DetachUnitView(unit.RuntimeId);
                if (view == null)
                {
                    continue;
                }

                view.Bind(unit, boardPresenter.GetWorldPosition(unit.BattleCoord));
                RemoveDuplicateUnitViews(unit.RuntimeId, view);
                TrackUnitView(unit.RuntimeId, view);
                BindStatusOverlay(unit, view);
            }
        }

        private void BindStatusOverlay(RuntimeUnit unit, UnitView view)
        {
            if (statusOverlayController == null)
            {
                return;
            }

            if (unit == null || unit.CurrentHp <= 0)
            {
                if (unit != null)
                {
                    statusOverlayController.Release(unit.RuntimeId);
                }

                return;
            }

            statusOverlayController.BindRuntimeUnit(unit, view);
        }

        private void TrackUnitView(int runtimeId, UnitView view)
        {
            if (view == null)
            {
                return;
            }

            UnitView trackedView;
            if (unitViewByRuntimeId.TryGetValue(runtimeId, out trackedView))
            {
                if (trackedView == view)
                {
                    if (!unitViews.Contains(view))
                    {
                        unitViews.Add(view);
                    }

                    return;
                }

                if (trackedView != null)
                {
                    trackedView.gameObject.SetActive(false);
                    Destroy(trackedView.gameObject);
                    unitViews.Remove(trackedView);
                }

                unitViewByRuntimeId[runtimeId] = view;
            }
            else
            {
                unitViewByRuntimeId.Add(runtimeId, view);
            }

            if (!unitViews.Contains(view))
            {
                unitViews.Add(view);
            }
        }

        private bool TryFindExistingUnitView(int runtimeId, out UnitView view)
        {
            view = null;
            Transform searchRoot = unitRoot != null ? unitRoot : transform;
            searchRoot.GetComponentsInChildren(true, unitViewSearchBuffer);
            for (int i = 0; i < unitViewSearchBuffer.Count; i++)
            {
                UnitView candidate = unitViewSearchBuffer[i];
                if (candidate == null || candidate.RuntimeId != runtimeId || unitViews.Contains(candidate))
                {
                    continue;
                }

                if (view == null || (!view.gameObject.activeInHierarchy && candidate.gameObject.activeInHierarchy))
                {
                    view = candidate;
                }
            }

            unitViewSearchBuffer.Clear();
            return view != null;
        }

        private void RemoveDuplicateUnitViews(int runtimeId, UnitView retainedView)
        {
            Transform searchRoot = unitRoot != null ? unitRoot : transform;
            searchRoot.GetComponentsInChildren(true, unitViewSearchBuffer);
            for (int i = 0; i < unitViewSearchBuffer.Count; i++)
            {
                UnitView candidate = unitViewSearchBuffer[i];
                if (candidate == null || candidate == retainedView || candidate.RuntimeId != runtimeId)
                {
                    continue;
                }

                candidate.gameObject.SetActive(false);
                Destroy(candidate.gameObject);
            }

            unitViewSearchBuffer.Clear();
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
