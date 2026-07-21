using System;

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
                if (card == null || card.UnitDefinition == null)
                {
                    continue;
                }

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

        public static void MarkPlayerReady(BattleState battleState)
        {
            MarkReady(battleState, BattleSide.Player);
        }

        public static void MarkEnemyReady(BattleState battleState)
        {
            MarkReady(battleState, BattleSide.Enemy);
        }

        public static bool TryStartCombatIfReady(BattleState battleState)
        {
            if (!CanAdvancePreparation(battleState))
            {
                return false;
            }

            if (!battleState.Player.IsReady || !battleState.Enemy.IsReady)
            {
                return false;
            }

            battleState.StopPreparationCountdown();
            battleState.Phase = BattlePhase.Combat;
            return true;
        }

        public static void CompleteActiveSideAction(BattleState battleState)
        {
            TryStartCombatIfReady(battleState);
        }

        public static void MarkActiveSideReadyAndAdvance(BattleState battleState)
        {
            if (!CanAdvancePreparation(battleState))
            {
                return;
            }

            MarkReady(battleState, battleState.ActivePreparationSide);
        }

        public static void EnsureActiveSideCanAct(BattleState battleState)
        {
            TryStartCombatIfReady(battleState);
        }

        public static bool HasOnlyRepositionActions(BattleState battleState)
        {
            if (battleState == null || battleState.Phase != BattlePhase.Preparation)
            {
                return false;
            }

            return !CanPlayAnyUnit(battleState, battleState.Player) && !CanPlayAnyUnit(battleState, battleState.Enemy);
        }

        public static bool ShouldStartPreparationCountdown(BattleState battleState)
        {
            if (battleState == null || battleState.Phase != BattlePhase.Preparation || battleState.PreparationCountdownActive)
            {
                return false;
            }

            if (battleState.Player.IsReady && battleState.Enemy.IsReady)
            {
                return false;
            }

            return (battleState.Player.IsReady || battleState.Enemy.IsReady) && HasOnlyRepositionActions(battleState);
        }

        private static bool CanSidePrepare(BattleState battleState, BattleSide side)
        {
            if (battleState == null || battleState.Phase != BattlePhase.Preparation)
            {
                return false;
            }

            return !battleState.GetPlayerState(side).IsReady;
        }

        private static void MarkReady(BattleState battleState, BattleSide side)
        {
            if (!CanSidePrepare(battleState, side))
            {
                return;
            }

            battleState.GetPlayerState(side).IsReady = true;
            battleState.StopPreparationCountdown();
            TryStartCombatIfReady(battleState);
        }

        private static bool CanAdvancePreparation(BattleState battleState)
        {
            return battleState != null && battleState.Phase == BattlePhase.Preparation;
        }
    }
}
