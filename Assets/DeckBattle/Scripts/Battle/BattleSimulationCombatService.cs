using System;

namespace DeckBattle
{
    public static class BattleSimulationCombatService
    {
        public static CombatSimulationResult RunToResolution(
            BattleState state,
            float tickDuration,
            int maxTicks,
            BattleRuntimeTuning tuning,
            BattleEventQueue eventQueue)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            BattleSimulation simulation = BattleSimulationFactory.Create(state, tuning);
            var tickLoop = new BattleTickLoop(simulation, tickDuration);
            return RunToResolution(state, simulation, tickLoop, maxTicks, eventQueue);
        }

        public static CombatSimulationResult RunToResolution(
            BattleState state,
            BattleSimulation simulation,
            BattleTickLoop tickLoop,
            int maxTicks,
            BattleEventQueue eventQueue)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (tickLoop == null)
            {
                throw new ArgumentNullException(nameof(tickLoop));
            }

            if (eventQueue == null)
            {
                throw new ArgumentNullException(nameof(eventQueue));
            }

            if (maxTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTicks));
            }

            int ticks = 0;
            while (state.Phase == BattlePhase.Combat && ticks < maxTicks)
            {
                BattleTickResult tickResult = tickLoop.Tick(simulation, eventQueue);
                ticks++;
                if (tickResult.BattleEnded)
                {
                    state.Phase = BattlePhase.RoundResolution;
                    BattleSimulationResultApplier.Apply(state, simulation);
                    return CreateCombatResult(tickResult, ticks);
                }
            }

            if (state.Phase == BattlePhase.Combat)
            {
                state.Phase = BattlePhase.RoundResolution;
            }

            BattleSimulationResultApplier.Apply(state, simulation);
            return CombatSimulationResult.MaxTicksReached(ticks);
        }

        public static CombatSimulationResult CreateCombatResult(BattleTickResult tickResult, int ticks)
        {
            CombatEndReason endReason = tickResult.HasWinner
                ? CombatEndReason.OneSideDefeated
                : CombatEndReason.BothSidesDefeated;
            return CombatSimulationResult.Ended(ticks, tickResult.HasWinner, tickResult.Winner, endReason);
        }
    }
}
