namespace DeckBattle
{
    public static class PreparationTurnService
    {
        public static bool CanPlayerPrepare(BattleState battleState)
        {
            return CanSidePrepare(battleState, BattleSide.Player);
        }

        public static bool CanEnemyPrepare(BattleState battleState)
        {
            return CanSidePrepare(battleState, BattleSide.Enemy);
        }

        public static bool CanSidePrepare(BattleState battleState, BattleSide side)
        {
            if (battleState == null || battleState.Phase != BattlePhase.Preparation)
            {
                return false;
            }

            return battleState.ActivePreparationSide == side
                && !battleState.GetPlayerState(side).IsReady;
        }

        public static bool TryConfirmReady(BattleState battleState, BattleSide side)
        {
            if (!CanSidePrepare(battleState, side))
            {
                return false;
            }

            battleState.GetPlayerState(side).IsReady = true;

            BattleSide oppositeSide = BattleState.GetOppositeSide(side);
            if (battleState.GetPlayerState(oppositeSide).IsReady)
            {
                battleState.Phase = BattlePhase.Combat;
                return true;
            }

            battleState.ActivePreparationSide = oppositeSide;
            return true;
        }

        public static bool MarkPlayerReady(BattleState battleState)
        {
            return TryConfirmReady(battleState, BattleSide.Player);
        }

        public static bool MarkEnemyReady(BattleState battleState)
        {
            return TryConfirmReady(battleState, BattleSide.Enemy);
        }
    }
}
