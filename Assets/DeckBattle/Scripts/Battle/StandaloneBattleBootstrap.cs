using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class StandaloneBattleBootstrap : MonoBehaviour
    {
        [SerializeField] private BattleConfig battleConfig;
        [SerializeField] private BattleTimingConfig battleTimingConfig;
        [SerializeField] private BattleView battleView;
        [SerializeField] private BattleCombatRunner combatRunner;
        [SerializeField] private float tickDuration = BattleTiming.DefaultCombatTickDuration;
        [SerializeField] private int maxTicks = BattleTiming.DefaultMaxCombatTicks;
        [SerializeField] private int maxTicksPerFrame = BattleTiming.DefaultMaxTicksPerFrame;
        [SerializeField] private bool startOnAwake;
        [SerializeField] private List<SpawnEntry> initialUnits = new List<SpawnEntry>(8);

        private readonly List<UnitSpawnData> spawnBuffer = new List<UnitSpawnData>(16);

        private void Awake()
        {
            if (battleView == null)
            {
                battleView = GetComponent<BattleView>();
            }

            if (combatRunner == null)
            {
                combatRunner = GetComponent<BattleCombatRunner>();
            }

            if (startOnAwake)
            {
                StartConfiguredBattle();
            }
        }

        private void OnDisable()
        {
            if (combatRunner == null)
            {
                return;
            }

            if (battleView != null)
            {
                combatRunner.TickProcessed -= battleView.ProcessCombatTick;
            }

            combatRunner.StopCombat();
        }

        public void StartConfiguredBattle()
        {
            if (battleConfig == null || battleView == null || combatRunner == null || initialUnits.Count == 0)
            {
                Debug.LogWarning("Standalone battle bootstrap is missing required configuration.", this);
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

            if (spawnBuffer.Count == 0)
            {
                return;
            }

            if (battleConfig.RuntimeTuningConfig == null)
            {
                Debug.LogWarning("Standalone battle bootstrap requires BattleConfig.RuntimeTuningConfig.", this);
                return;
            }

            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(battleConfig.BoardWidth, battleConfig.BoardHeight, 1f),
                spawnBuffer,
                battleConfig.RuntimeTuningConfig.CreateRuntimeTuning());
            float resolvedTickDuration = battleTimingConfig != null ? battleTimingConfig.CombatTickDuration : tickDuration;
            int resolvedMaxTicks = battleTimingConfig != null ? battleTimingConfig.MaxCombatTicks : maxTicks;
            int resolvedMaxTicksPerFrame = battleTimingConfig != null ? battleTimingConfig.MaxTicksPerFrame : maxTicksPerFrame;

            combatRunner.TickProcessed -= battleView.ProcessCombatTick;
            combatRunner.StartCombat(
                simulation,
                Mathf.Max(BattleTiming.MinCombatTickDuration, resolvedTickDuration),
                Mathf.Max(1, resolvedMaxTicks),
                Mathf.Max(1, resolvedMaxTicksPerFrame));
            battleView.BindInitialState(combatRunner.PresentationSnapshot);
            combatRunner.TickProcessed += battleView.ProcessCombatTick;
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
