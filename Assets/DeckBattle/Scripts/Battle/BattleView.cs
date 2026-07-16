using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleView : MonoBehaviour
    {
        public event Action<BattleTickResult, int> TickProcessed;

        [Header("Simulation")]
        [SerializeField] private BattleConfig battleConfig;
        [SerializeField] private float tickDuration = 0.2f;
        [SerializeField] private int maxTicksPerFrame = 4;
        [SerializeField] private float attackCooldownMultiplier = 1f;
        [SerializeField] private int attackRangeBonus;
        [SerializeField] private int movementStepsPerTick = 1;
        [SerializeField] private bool startOnAwake;
        [SerializeField] private List<SpawnEntry> initialUnits = new List<SpawnEntry>(8);

        [Header("Presentation")]
        [SerializeField] private BoardPresenter boardPresenter;
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private Transform unitRoot;
        [SerializeField] private PooledBattleEffect attackEffectPrefab;
        [SerializeField] private PooledBattleEffect damageEffectPrefab;
        [SerializeField] private Transform effectRoot;

        private readonly List<UnitSpawnData> spawnBuffer = new List<UnitSpawnData>(16);
        private readonly List<UnitView> activeUnitViews = new List<UnitView>(16);
        private readonly List<UnitView> unitViewSearchBuffer = new List<UnitView>(16);
        private readonly Dictionary<int, UnitView> unitViewByUnitId = new Dictionary<int, UnitView>(16);
        private readonly BattleEventQueue eventQueue = new BattleEventQueue(32);
        private readonly List<PooledBattleEffect> activeAttackEffects = new List<PooledBattleEffect>(8);
        private readonly List<PooledBattleEffect> activeDamageEffects = new List<PooledBattleEffect>(8);
        private readonly Stack<PooledBattleEffect> pooledAttackEffects = new Stack<PooledBattleEffect>(8);
        private readonly Stack<PooledBattleEffect> pooledDamageEffects = new Stack<PooledBattleEffect>(8);
        private readonly BattleDebugSnapshot debugSnapshot = new BattleDebugSnapshot(16);

        private BattleSimulation simulation;
        private BattleTickLoop tickLoop;
        private float tickAccumulator;
        private int ticksElapsed;
        private int maxSimulationTicks = int.MaxValue;
        private bool maxTicksReached;
        private BattleTickResult lastTickResult;

        public BattleSimulation Simulation
        {
            get { return simulation; }
        }

        public BoardPresenter BoardPresenter
        {
            get { return boardPresenter; }
        }

        public BattleDebugSnapshot DebugSnapshot
        {
            get { return debugSnapshot; }
        }

        public int TicksElapsed
        {
            get { return ticksElapsed; }
        }

        public bool MaxTicksReached
        {
            get { return maxTicksReached; }
        }

        public BattleTickResult LastTickResult
        {
            get { return lastTickResult; }
        }

        private void OnValidate()
        {
            tickDuration = Mathf.Max(0.01f, tickDuration);
            maxTicksPerFrame = Mathf.Max(1, maxTicksPerFrame);
            attackCooldownMultiplier = Mathf.Max(0.01f, attackCooldownMultiplier);
            movementStepsPerTick = Mathf.Max(1, movementStepsPerTick);
        }

        private void Awake()
        {
            if (startOnAwake)
            {
                StartConfiguredBattle();
            }
        }

        private void Update()
        {
            UpdateSimulation(Time.deltaTime);
            ReleaseCompletedEffects(activeAttackEffects, pooledAttackEffects);
            ReleaseCompletedEffects(activeDamageEffects, pooledDamageEffects);
        }

        public void StartConfiguredBattle()
        {
            if (initialUnits.Count == 0)
            {
                Debug.LogWarning("BattleView has no configured realtime units to spawn.", this);
                return;
            }

            spawnBuffer.Clear();
            int nextGeneratedUnitId = 1;
            for (int i = 0; i < initialUnits.Count; i++)
            {
                SpawnEntry entry = initialUnits[i];
                if (entry == null || entry.Definition == null)
                {
                    continue;
                }

                int unitId = entry.UnitId > 0 ? entry.UnitId : nextGeneratedUnitId;
                spawnBuffer.Add(new UnitSpawnData(unitId, entry.Definition, entry.Side, entry.ToHexCoord()));
                nextGeneratedUnitId = Mathf.Max(nextGeneratedUnitId, unitId) + 1;
            }

            StartBattle(spawnBuffer);
        }

        public void StartBattle(IList<UnitSpawnData> spawnData)
        {
            if (battleConfig == null || boardPresenter == null || unitPrefab == null)
            {
                Debug.LogError("BattleView is missing required references.", this);
                return;
            }

            if (spawnData == null)
            {
                throw new ArgumentNullException(nameof(spawnData));
            }

            HexBoard board = new HexBoard(battleConfig.BoardWidth, battleConfig.BoardHeight, 1f);
            BattleSimulation nextSimulation = BattleSimulation.Create(board, spawnData, CreateRuntimeTuning());
            BindSimulation(nextSimulation);
        }

        public void BindSimulation(BattleSimulation nextSimulation)
        {
            BindSimulation(nextSimulation, tickDuration, int.MaxValue);
        }

        public void BindSimulation(BattleSimulation nextSimulation, float nextTickDuration, int nextMaxTicks)
        {
            BindSimulation(nextSimulation, nextTickDuration, nextMaxTicks, null);
        }

        public void BindSimulation(
            BattleSimulation nextSimulation,
            float nextTickDuration,
            int nextMaxTicks,
            IReadOnlyDictionary<int, UnitView> reusableUnitViews)
        {
            if (nextSimulation == null)
            {
                throw new ArgumentNullException(nameof(nextSimulation));
            }

            if (boardPresenter == null || unitPrefab == null)
            {
                Debug.LogError("BattleView is missing required presentation references.", this);
                return;
            }

            simulation = nextSimulation;
            float resolvedTickDuration = Mathf.Max(0.01f, nextTickDuration);
            tickLoop = new BattleTickLoop(simulation, resolvedTickDuration);
            tickAccumulator = 0f;
            ticksElapsed = 0;
            maxSimulationTicks = Mathf.Max(1, nextMaxTicks);
            maxTicksReached = false;
            lastTickResult = new BattleTickResult(0, 0, false, false, BattleSide.Player);

            boardPresenter.Build(simulation.Board);
            ReleaseAllUnitViews(reusableUnitViews);
            debugSnapshot.Capture(simulation, null);
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit != null && unit.IsAlive)
                {
                    CreateOrUpdateUnitView(unit, reusableUnitViews);
                }
            }
        }

        public UnitView DetachUnitView(int unitId)
        {
            UnitView view;
            if (!unitViewByUnitId.TryGetValue(unitId, out view) || view == null)
            {
                return null;
            }

            unitViewByUnitId.Remove(unitId);
            activeUnitViews.Remove(view);
            return view;
        }

        public void ClearBattle()
        {
            ClearBattle(true);
        }

        public void ClearBattle(bool releaseUnitViews)
        {
            simulation = null;
            tickLoop = null;
            tickAccumulator = 0f;
            ticksElapsed = 0;
            maxSimulationTicks = int.MaxValue;
            maxTicksReached = false;
            lastTickResult = new BattleTickResult(0, 0, false, false, BattleSide.Player);
            eventQueue.Clear();
            debugSnapshot.Capture(null, null);
            if (releaseUnitViews)
            {
                ReleaseAllUnitViews(null);
            }
            else
            {
                activeUnitViews.Clear();
                unitViewByUnitId.Clear();
            }

            ReleaseAllEffects(activeAttackEffects, pooledAttackEffects);
            ReleaseAllEffects(activeDamageEffects, pooledDamageEffects);
        }

        private void UpdateSimulation(float deltaTime)
        {
            if (simulation == null || tickLoop == null || simulation.IsBattleEnded)
            {
                return;
            }

            tickAccumulator += deltaTime;
            int ticksThisFrame = 0;
            while (tickAccumulator >= tickLoop.TickDuration && ticksThisFrame < maxTicksPerFrame)
            {
                if (ticksElapsed >= maxSimulationTicks)
                {
                    StopTickingBecauseMaxTicksReached();
                    return;
                }

                BattleTickResult result = tickLoop.Tick(simulation, eventQueue);
                ticksElapsed++;
                lastTickResult = result;
                debugSnapshot.Capture(simulation, eventQueue.Events);
                ProcessEvents(eventQueue.Events);
                if (TickProcessed != null)
                {
                    TickProcessed.Invoke(result, ticksElapsed);
                }

                tickAccumulator -= tickLoop.TickDuration;
                ticksThisFrame++;

                if (result.BattleEnded)
                {
                    tickAccumulator = 0f;
                    return;
                }

                if (ticksElapsed >= maxSimulationTicks)
                {
                    StopTickingBecauseMaxTicksReached();
                    return;
                }
            }

            if (ticksThisFrame >= maxTicksPerFrame)
            {
                tickAccumulator = 0f;
            }
        }

        private void StopTickingBecauseMaxTicksReached()
        {
            maxTicksReached = true;
            tickLoop = null;
            tickAccumulator = 0f;
        }

        private void ProcessEvents(IReadOnlyList<BattleEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                switch (battleEvent.Type)
                {
                    case BattleEventType.UnitMoved:
                        HandleUnitMoved(battleEvent);
                        break;
                    case BattleEventType.UnitAttackStarted:
                        HandleUnitAttackStarted(battleEvent);
                        break;
                    case BattleEventType.UnitDamaged:
                        HandleUnitDamaged(battleEvent);
                        break;
                    case BattleEventType.UnitDied:
                        HandleUnitDied(battleEvent);
                        break;
                }
            }
        }

        private void HandleUnitMoved(BattleEvent battleEvent)
        {
            UnitView view;
            if (!unitViewByUnitId.TryGetValue(battleEvent.UnitId, out view) || view == null)
            {
                return;
            }

            view.MoveToWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To), tickLoop.TickDuration);
        }

        private void HandleUnitAttackStarted(BattleEvent battleEvent)
        {
            UnitView attackerView;
            if (unitViewByUnitId.TryGetValue(battleEvent.UnitId, out attackerView) && attackerView != null)
            {
                attackerView.PlayAttack();
                UnitRuntimeState attacker;
                if (simulation.TryGetUnitById(battleEvent.UnitId, out attacker))
                {
                    SpawnEffect(attackEffectPrefab, boardPresenter.GetWorldPosition(attacker.CurrentHex), activeAttackEffects, pooledAttackEffects);
                }
            }
        }

        private void HandleUnitDamaged(BattleEvent battleEvent)
        {
            UnitView targetView;
            if (!unitViewByUnitId.TryGetValue(battleEvent.UnitId, out targetView) || targetView == null)
            {
                return;
            }

            targetView.PlayDamage(battleEvent.RemainingHp);
            UnitRuntimeState target;
            if (simulation.TryGetUnitById(battleEvent.UnitId, out target))
            {
                SpawnEffect(damageEffectPrefab, boardPresenter.GetWorldPosition(target.CurrentHex), activeDamageEffects, pooledDamageEffects);
            }
        }

        private void HandleUnitDied(BattleEvent battleEvent)
        {
            UnitView view;
            if (!unitViewByUnitId.TryGetValue(battleEvent.UnitId, out view) || view == null)
            {
                return;
            }

            view.PlayDeath();
        }

        private void CreateOrUpdateUnitView(UnitRuntimeState unit)
        {
            CreateOrUpdateUnitView(unit, null);
        }

        private void CreateOrUpdateUnitView(UnitRuntimeState unit, IReadOnlyDictionary<int, UnitView> reusableUnitViews)
        {
            UnitView view;
            if (unitViewByUnitId.TryGetValue(unit.UnitId, out view) && view != null)
            {
                view.Bind(unit, boardPresenter.GetWorldPosition(unit.CurrentHex));
                return;
            }

            if (reusableUnitViews != null && reusableUnitViews.TryGetValue(unit.UnitId, out view) && view != null)
            {
                RemoveDuplicateSceneUnitViews(unit.UnitId, view);
            }
            else if (TryFindReusableUnitView(unit.UnitId, out view))
            {
                RemoveDuplicateSceneUnitViews(unit.UnitId, view);
            }
            else
            {
                view = GetUnitView();
            }

            view.Bind(unit, boardPresenter.GetWorldPosition(unit.CurrentHex));
            activeUnitViews.Add(view);
            unitViewByUnitId.Add(unit.UnitId, view);
        }

        private UnitView GetUnitView()
        {
            Transform parent = unitRoot != null ? unitRoot : transform;
            return Instantiate(unitPrefab, parent);
        }

        private bool TryFindReusableUnitView(int unitId, out UnitView view)
        {
            view = null;
            Transform searchRoot = unitRoot != null ? unitRoot : transform;
            searchRoot.GetComponentsInChildren(true, unitViewSearchBuffer);
            for (int i = 0; i < unitViewSearchBuffer.Count; i++)
            {
                UnitView candidate = unitViewSearchBuffer[i];
                if (candidate == null || candidate.RuntimeId != unitId || activeUnitViews.Contains(candidate))
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

        private void RemoveDuplicateSceneUnitViews(int unitId, UnitView retainedView)
        {
            Transform searchRoot = unitRoot != null ? unitRoot : transform;
            searchRoot.GetComponentsInChildren(true, unitViewSearchBuffer);
            for (int i = 0; i < unitViewSearchBuffer.Count; i++)
            {
                UnitView candidate = unitViewSearchBuffer[i];
                if (candidate == null || candidate == retainedView || candidate.RuntimeId != unitId)
                {
                    continue;
                }

                candidate.gameObject.SetActive(false);
                Destroy(candidate.gameObject);
            }

            unitViewSearchBuffer.Clear();
        }

        private void ReleaseAllUnitViews(IReadOnlyDictionary<int, UnitView> retainedUnitViews)
        {
            for (int i = activeUnitViews.Count - 1; i >= 0; i--)
            {
                UnitView view = activeUnitViews[i];
                if (view == null)
                {
                    continue;
                }

                if (IsRetainedUnitView(view, retainedUnitViews))
                {
                    continue;
                }

                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }

            activeUnitViews.Clear();
            unitViewByUnitId.Clear();
        }

        private static bool IsRetainedUnitView(UnitView view, IReadOnlyDictionary<int, UnitView> retainedUnitViews)
        {
            if (view == null || retainedUnitViews == null)
            {
                return false;
            }

            UnitView retainedView;
            return retainedUnitViews.TryGetValue(view.RuntimeId, out retainedView) && retainedView == view;
        }

        private void SpawnEffect(
            PooledBattleEffect prefab,
            Vector3 position,
            List<PooledBattleEffect> activeEffects,
            Stack<PooledBattleEffect> pooledEffects)
        {
            if (prefab == null)
            {
                return;
            }

            PooledBattleEffect effect = pooledEffects.Count > 0 ? pooledEffects.Pop() : Instantiate(prefab, effectRoot != null ? effectRoot : transform);
            effect.Play(position);
            activeEffects.Add(effect);
        }

        private BattleRuntimeTuning CreateRuntimeTuning()
        {
            return new BattleRuntimeTuning(attackCooldownMultiplier, attackRangeBonus, movementStepsPerTick);
        }

        private static void ReleaseCompletedEffects(List<PooledBattleEffect> activeEffects, Stack<PooledBattleEffect> pooledEffects)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                PooledBattleEffect effect = activeEffects[i];
                if (effect != null && effect.IsPlaying)
                {
                    continue;
                }

                if (effect != null)
                {
                    effect.gameObject.SetActive(false);
                    pooledEffects.Push(effect);
                }

                activeEffects.RemoveAt(i);
            }
        }

        private static void ReleaseAllEffects(List<PooledBattleEffect> activeEffects, Stack<PooledBattleEffect> pooledEffects)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                PooledBattleEffect effect = activeEffects[i];
                if (effect == null)
                {
                    continue;
                }

                effect.gameObject.SetActive(false);
                pooledEffects.Push(effect);
            }

            activeEffects.Clear();
        }

        [Serializable]
        private sealed class SpawnEntry
        {
            public int UnitId;
            public UnitDefinition Definition;
            public BattleSide Side;
            public int Q;
            public int R;

            public HexCoord ToHexCoord()
            {
                return new HexCoord(Q, R);
            }
        }
    }
}
