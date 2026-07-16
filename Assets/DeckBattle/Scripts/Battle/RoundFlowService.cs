using System;

namespace DeckBattle
{
    public static class RoundFlowService
    {
        public static RoundResolutionResult ResolveRoundAndStartNext(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            RoundResolutionResult result = RoundDamageResolver.Resolve(battleState);
            if (!result.MatchEnded)
            {
                battleState.StartNextRound();
            }
            else
            {
                FormationService.RestoreFormationAndResetRoundHealth(battleState.Player);
                FormationService.RestoreFormationAndResetRoundHealth(battleState.Enemy);
            }

            return result;
        }
    }
}
