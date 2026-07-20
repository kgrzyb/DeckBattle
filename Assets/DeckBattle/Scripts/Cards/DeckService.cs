using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class DeckService
    {
        public static void CreateDeck(IList<UnitDefinition> definitions, IList<CardRuntimeState> targetDeck, ref int nextRuntimeCardId)
        {
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                UnitDefinition definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                targetDeck.Add(new CardRuntimeState(nextRuntimeCardId, definition));
                nextRuntimeCardId++;
            }
        }

        public static void Shuffle(IList<CardRuntimeState> deck, DeterministicRandom rng)
        {
            if (deck == null)
            {
                throw new ArgumentNullException(nameof(deck));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            for (int i = deck.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.NextInt(0, i + 1);
                CardRuntimeState temp = deck[i];
                deck[i] = deck[swapIndex];
                deck[swapIndex] = temp;
            }
        }

        public static int DrawCards(PlayerBattleState player, int count, int maxHandSize = int.MaxValue)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            int safeMaxHandSize = Math.Max(0, maxHandSize);
            int drawn = 0;
            for (int i = 0; i < count && player.Deck.Count > 0 && player.Hand.Count < safeMaxHandSize; i++)
            {
                int lastIndex = player.Deck.Count - 1;
                CardRuntimeState card = player.Deck[lastIndex];
                player.Deck.RemoveAt(lastIndex);
                card.Location = CardLocation.Hand;
                player.Hand.Add(card);
                drawn++;
            }

            return drawn;
        }
    }
}
