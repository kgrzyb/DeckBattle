using System;

namespace DeckBattle
{
    public static class RoundDamageResolver
    {
        public static RoundResolutionResult Resolve(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (battleState.Phase != BattlePhase.RoundResolution)
            {
                throw new InvalidOperationException("Round damage can only be resolved during RoundResolution.");
            }

            int playerDamage = CalculateSurvivorPower(battleState.Player);
            int enemyDamage = CalculateSurvivorPower(battleState.Enemy);

            battleState.Enemy.Hp = Math.Max(0, battleState.Enemy.Hp - playerDamage);
            battleState.Player.Hp = Math.Max(0, battleState.Player.Hp - enemyDamage);

            bool playerDefeated = battleState.Player.Hp <= 0;
            bool enemyDefeated = battleState.Enemy.Hp <= 0;
            bool matchEnded = playerDefeated || enemyDefeated;
            bool hasWinner = playerDefeated != enemyDefeated;
            BattleSide winner = enemyDefeated ? BattleSide.Player : BattleSide.Enemy;

            if (matchEnded)
            {
                battleState.Phase = BattlePhase.MatchEnd;
            }

            return new RoundResolutionResult(
                playerDamage,
                enemyDamage,
                battleState.Player.Hp,
                battleState.Enemy.Hp,
                matchEnded,
                hasWinner,
                winner);
        }

        private static int CalculateSurvivorPower(PlayerBattleState player)
        {
            int power = 0;
            for (int i = 0; i < player.Units.Count; i++)
            {
                RuntimeUnit unit = player.Units[i];
                if (unit == null || !unit.IsAlive || unit.Definition == null)
                {
                    continue;
                }

                power += Math.Max(0, unit.Definition.Power);
            }

            return power;
        }
    }
}
