using System;

namespace DeckBattle
{
    public static class HandService
    {
        public static bool CanPayForCard(PlayerBattleState player, CardRuntimeState card)
        {
            if (player == null || card == null || card.Definition == null)
            {
                return false;
            }

            return player.Ap >= card.Definition.ApCost;
        }

        public static bool IsInHand(PlayerBattleState player, CardRuntimeState card)
        {
            if (player == null || card == null || card.Location != CardLocation.Hand)
            {
                return false;
            }

            return player.Hand.Contains(card);
        }

        public static void RemoveFromHand(PlayerBattleState player, CardRuntimeState card)
        {
            if (!IsInHand(player, card))
            {
                throw new InvalidOperationException("Card is not in hand.");
            }

            player.Hand.Remove(card);
        }
    }
}
