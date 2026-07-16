using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleView : MonoBehaviour
    {
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
        private readonly Stack<UnitView> pooledUnitViews = new Stack<UnitView>(16);
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
            tickLoop = new BattleTickLoop(simulation, Mathf.Max(0.01f, tickDuration));
            tickAccumulator = 0f;

            boardPresenter.Build(simulation.Board);
            ReleaseAllUnitViews();
            debugSnapshot.Capture(simulation, null);
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit != null && unit.IsAlive)
                {
                    CreateOrUpdateUnitView(unit);
                }
            }
        }

        public void ClearBattle()
        {
            simulation = null;
            tickLoop = null;
            tickAccumulator = 0f;
            eventQueue.Clear();
            debugSnapshot.Capture(null, null);
            ReleaseAllUnitViews();
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
                BattleTickResult result = tickLoop.Tick(simulation, eventQueue);
                debugSnapshot.Capture(simulation, eventQueue.Events);
                ProcessEvents(eventQueue.Events);
                tickAccumulator -= tickLoop.TickDuration;
                ticksThisFrame++;

                if (result.BattleEnded)
                {
                    tickAccumulator = 0f;
                    return;
                }
            }

            if (ticksThisFrame >= maxTicksPerFrame)
            {
                tickAccumulator = 0f;
            }
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
            unitViewByUnitId.Remove(battleEvent.UnitId);
        }

        private void CreateOrUpdateUnitView(UnitRuntimeState unit)
        {
            UnitView view;
            if (unitViewByUnitId.TryGetValue(unit.UnitId, out view) && view != null)
            {
                view.Bind(unit, boardPresenter.GetWorldPosition(unit.CurrentHex));
                return;
            }

            view = GetUnitView();
            view.Bind(unit, boardPresenter.GetWorldPosition(unit.CurrentHex));
            activeUnitViews.Add(view);
            unitViewByUnitId.Add(unit.UnitId, view);
        }

        private UnitView GetUnitView()
        {
            if (pooledUnitViews.Count > 0)
            {
                return pooledUnitViews.Pop();
            }

            Transform parent = unitRoot != null ? unitRoot : transform;
            return Instantiate(unitPrefab, parent);
        }

        private void ReleaseAllUnitViews()
        {
            for (int i = activeUnitViews.Count - 1; i >= 0; i--)
            {
                UnitView view = activeUnitViews[i];
                if (view == null)
                {
                    continue;
                }

                view.gameObject.SetActive(false);
                pooledUnitViews.Push(view);
            }

            activeUnitViews.Clear();
            unitViewByUnitId.Clear();
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
