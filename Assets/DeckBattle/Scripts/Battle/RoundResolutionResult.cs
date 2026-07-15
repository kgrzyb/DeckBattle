namespace DeckBattle
{
    public sealed class RoundResolutionResult
    {
        public readonly int PlayerDamageDealt;
        public readonly int EnemyDamageDealt;
        public readonly int PlayerHpAfter;
        public readonly int EnemyHpAfter;
        public readonly bool MatchEnded;
        public readonly bool HasWinner;
        public readonly BattleSide Winner;

        public RoundResolutionResult(
            int playerDamageDealt,
            int enemyDamageDealt,
            int playerHpAfter,
            int enemyHpAfter,
            bool matchEnded,
            bool hasWinner,
            BattleSide winner)
        {
            PlayerDamageDealt = playerDamageDealt;
            EnemyDamageDealt = enemyDamageDealt;
            PlayerHpAfter = playerHpAfter;
            EnemyHpAfter = enemyHpAfter;
            MatchEnded = matchEnded;
            HasWinner = hasWinner;
            Winner = winner;
        }
    }
}
