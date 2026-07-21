using System;

namespace DeckBattle
{
    public static class BattleSimulationResultApplier
    {
        public static void Apply(BattleState state, BattleSimulation simulation)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            ApplyUnits(state.Player, simulation);
            ApplyUnits(state.Enemy, simulation);
        }

        private static void ApplyUnits(PlayerBattleState player, BattleSimulation simulation)
        {
            for (int i = 0; i < player.Units.Count; i++)
            {
                RuntimeUnit runtimeUnit = player.Units[i];
                if (runtimeUnit == null)
                {
                    continue;
                }

                UnitRuntimeState simulatedUnit;
                if (!simulation.TryGetUnitById(runtimeUnit.RuntimeId, out simulatedUnit) || simulatedUnit == null)
                {
                    continue;
                }

                runtimeUnit.CurrentHp = Math.Max(0, simulatedUnit.CurrentHp);
                runtimeUnit.BattleCoord = simulatedUnit.CurrentHex;
                runtimeUnit.IsDefeated = simulatedUnit.IsDefeated || runtimeUnit.CurrentHp <= 0;
                runtimeUnit.AttackBonusNextCombat = 0;
            }
        }
    }
}
