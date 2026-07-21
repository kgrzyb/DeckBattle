using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class BattleSimulationFactory
    {
        public static BattleSimulation Create(BattleState state)
        {
            return Create(state, BattleRuntimeTuning.Default);
        }

        public static BattleSimulation Create(BattleState state, BattleRuntimeTuning tuning)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var spawnData = new List<UnitSpawnData>(state.Player.Units.Count + state.Enemy.Units.Count);
            AddUnits(state.Player.Units, spawnData);
            AddUnits(state.Enemy.Units, spawnData);
            return BattleSimulation.Create(state.Board, spawnData, tuning);
        }

        private static void AddUnits(IList<RuntimeUnit> units, List<UnitSpawnData> spawnData)
        {
            for (int i = 0; i < units.Count; i++)
            {
                RuntimeUnit unit = units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                spawnData.Add(new UnitSpawnData(unit.RuntimeId, unit.Definition, unit.Side, unit.BattleCoord, unit.AttackBonusNextCombat));
            }
        }
    }
}
