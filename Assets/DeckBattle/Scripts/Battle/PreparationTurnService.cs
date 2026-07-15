using System;

namespace DeckBattle
{
    public static class PreparationTurnService
    {
        public static bool CanPlayAnyUnit(BattleState battleState, PlayerBattleState player)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (player.IsReady || battleState.Phase != BattlePhase.Preparation)
            {
                return false;
            }

            for (int cardIndex = 0; cardIndex < player.Hand.Count; cardIndex++)
            {
                CardRuntimeState card = player.Hand[cardIndex];
                for (int r = 0; r < battleState.Board.Height; r++)
                {
                    for (int q = 0; q < battleState.Board.Width; q++)
                    {
                        if (UnitPlayService.ValidatePlay(battleState, player, card, new HexCoord(q, r)) == PlayUnitFailReason.None)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static void CompleteActiveSideAction(BattleState battleState)
        {
            if (!CanAdvancePreparation(battleState))
            {
                return;
            }

            PlayerBattleState activePlayer = battleState.GetPlayerState(battleState.ActivePreparationSide);
            MarkReadyIfNoLegalPlay(battleState, activePlayer);
            AdvanceToNextAvailableSide(battleState);
        }

        public static void MarkActiveSideReadyAndAdvance(BattleState battleState)
        {
            if (!CanAdvancePreparation(battleState))
            {
                return;
            }

            battleState.GetPlayerState(battleState.ActivePreparationSide).IsReady = true;
            AdvanceToNextAvailableSide(battleState);
        }

        public static void EnsureActiveSideCanAct(BattleState battleState)
        {
            if (!CanAdvancePreparation(battleState))
            {
                return;
            }

            PlayerBattleState activePlayer = battleState.GetPlayerState(battleState.ActivePreparationSide);
            MarkReadyIfNoLegalPlay(battleState, activePlayer);
            if (activePlayer.IsReady)
            {
                AdvanceToNextAvailableSide(battleState);
            }
        }

        private static bool CanAdvancePreparation(BattleState battleState)
        {
            return battleState != null && battleState.Phase == BattlePhase.Preparation;
        }

        private static void AdvanceToNextAvailableSide(BattleState battleState)
        {
            for (int i = 0; i < 2; i++)
            {
                if (battleState.Player.IsReady && battleState.Enemy.IsReady)
                {
                    battleState.Phase = BattlePhase.Combat;
                    return;
                }

                battleState.ActivePreparationSide = GetOppositeSide(battleState.ActivePreparationSide);
                PlayerBattleState nextPlayer = battleState.GetPlayerState(battleState.ActivePreparationSide);
                if (nextPlayer.IsReady)
                {
                    continue;
                }

                MarkReadyIfNoLegalPlay(battleState, nextPlayer);
                if (!nextPlayer.IsReady)
                {
                    return;
                }
            }

            if (battleState.Player.IsReady && battleState.Enemy.IsReady)
            {
                battleState.Phase = BattlePhase.Combat;
            }
        }

        private static void MarkReadyIfNoLegalPlay(BattleState battleState, PlayerBattleState player)
        {
            if (!CanPlayAnyUnit(battleState, player))
            {
                player.IsReady = true;
            }
        }

        private static BattleSide GetOppositeSide(BattleSide side)
        {
            return side == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;
        }
    }
}
