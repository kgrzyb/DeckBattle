namespace DeckBattle
{
    public enum CombatEndReason
    {
        None = 0,
        OneSideDefeated = 1,
        BothSidesDefeated = 2,
        MaxTicksReached = 3
    }

    public sealed class CombatSimulationResult
    {
        public readonly int Ticks;
        public readonly bool CombatEnded;
        public readonly bool HasWinner;
        public readonly BattleSide Winner;
        public readonly CombatEndReason EndReason;

        private CombatSimulationResult(int ticks, bool combatEnded, bool hasWinner, BattleSide winner, CombatEndReason endReason)
        {
            Ticks = ticks;
            CombatEnded = combatEnded;
            HasWinner = hasWinner;
            Winner = winner;
            EndReason = endReason;
        }

        public static CombatSimulationResult Ended(int ticks, bool hasWinner, BattleSide winner, CombatEndReason endReason)
        {
            return new CombatSimulationResult(ticks, true, hasWinner, winner, endReason);
        }

        public static CombatSimulationResult MaxTicksReached(int ticks)
        {
            return new CombatSimulationResult(ticks, true, false, BattleSide.Player, CombatEndReason.MaxTicksReached);
        }
    }
}
