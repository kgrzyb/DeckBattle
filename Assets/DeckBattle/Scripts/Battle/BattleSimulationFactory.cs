using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class BattleSimulationFactory
    {
        public static BattleSimulation Create(BattleState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            BattleRuntimeTuningConfig tuningConfig = state.Config != null ? state.Config.RuntimeTuningConfig : null;
            if (tuningConfig == null)
            {
                throw new InvalidOperationException("BattleConfig requires a BattleRuntimeTuningConfig to create a simulation.");
            }

            return Create(state, tuningConfig.CreateRuntimeTuning());
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
            BattleSimulation simulation = BattleSimulation.Create(state.Board, spawnData, tuning);
            ApplyPendingCombatEffects(state, simulation);
            InitializeAttackCooldowns(simulation);
            return simulation;
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

                spawnData.Add(new UnitSpawnData(
                    unit.RuntimeId,
                    UnitCombatSpec.FromDefinition(unit.Definition),
                    unit.Side,
                    unit.BattleCoord,
                    unit.AttackBonusNextCombat,
                    unit.Definition != null ? unit.Definition.DisplayName : null));
            }
        }

        private static void ApplyPendingCombatEffects(BattleState state, BattleSimulation simulation)
        {
            PendingCombatEffectQueue effects = state.PendingCombatEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                PendingCombatEffect effect = effects[i];
                if (effect.ScheduledRoundNumber != state.RoundNumber)
                {
                    continue;
                }

                CombatEffectResolver.TryResolveInitial(simulation, effect);
            }
        }

        private static void InitializeAttackCooldowns(BattleSimulation simulation)
        {
            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                unit.NextAttackTime = simulation.Tuning.GetAttackCooldown(unit.CombatSpec, unit);
            }
        }
    }
}
