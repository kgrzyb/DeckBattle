using System;

namespace DeckBattle
{
    public static class UnitPlayService
    {
        public static PlayUnitResult PlayUnit(BattleState battleState, PlayerBattleState player, CardRuntimeState card, HexCoord targetCoord)
        {
            PlayUnitFailReason failReason = ValidatePlay(battleState, player, card, targetCoord);
            if (failReason != PlayUnitFailReason.None)
            {
                return PlayUnitResult.Failed(failReason);
            }

            HandService.RemoveFromHand(player, card);
            player.Ap -= card.Definition.ApCost;
            card.Location = CardLocation.Played;
            player.PlayedCards.Add(card);

            var unit = new RuntimeUnit(battleState.AllocateRuntimeUnitId(), card.Definition, player.Side, targetCoord);
            player.Units.Add(unit);
            return PlayUnitResult.Succeeded(unit);
        }

        public static PlayUnitFailReason ValidatePlay(BattleState battleState, PlayerBattleState player, CardRuntimeState card, HexCoord targetCoord)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (battleState.Phase != BattlePhase.Preparation)
            {
                return PlayUnitFailReason.NotInPreparation;
            }

            if (player.IsReady)
            {
                return PlayUnitFailReason.PlayerReady;
            }

            if (!HandService.IsInHand(player, card))
            {
                return card != null && card.Location == CardLocation.Played ? PlayUnitFailReason.UnitAlreadyPlayed : PlayUnitFailReason.CardNotInHand;
            }

            if (WasUnitAlreadyPlayed(player, card.Definition))
            {
                return PlayUnitFailReason.UnitAlreadyPlayed;
            }

            if (!HandService.CanPayForCard(player, card))
            {
                return PlayUnitFailReason.NotEnoughAp;
            }

            if (player.Units.Count >= player.DeploymentSlots)
            {
                return PlayUnitFailReason.NoDeploymentSlot;
            }

            if (!battleState.Board.IsDeploymentCoord(player.Side, targetCoord))
            {
                return PlayUnitFailReason.InvalidTile;
            }

            if (FormationService.IsOccupied(player, targetCoord, null))
            {
                return PlayUnitFailReason.TileOccupied;
            }

            return PlayUnitFailReason.None;
        }

        private static bool WasUnitAlreadyPlayed(PlayerBattleState player, UnitDefinition definition)
        {
            for (int i = 0; i < player.PlayedCards.Count; i++)
            {
                if (player.PlayedCards[i].Definition == definition)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
