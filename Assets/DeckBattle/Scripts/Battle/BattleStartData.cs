using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleStartData
    {
        private readonly List<CardDefinition> playerDeck;
        private readonly List<CardDefinition> enemyDeck;

        public BattleStartData(
            IReadOnlyList<CardDefinition> playerDeck,
            IReadOnlyList<CardDefinition> enemyDeck,
            int seed)
        {
            if (playerDeck == null)
            {
                throw new ArgumentNullException(nameof(playerDeck));
            }

            if (enemyDeck == null)
            {
                throw new ArgumentNullException(nameof(enemyDeck));
            }

            this.playerDeck = new List<CardDefinition>(playerDeck.Count);
            this.enemyDeck = new List<CardDefinition>(enemyDeck.Count);
            CopyDeck(playerDeck, this.playerDeck);
            CopyDeck(enemyDeck, this.enemyDeck);
            Seed = seed;
        }

        public IReadOnlyList<CardDefinition> PlayerDeck
        {
            get { return playerDeck; }
        }

        public IReadOnlyList<CardDefinition> EnemyDeck
        {
            get { return enemyDeck; }
        }

        public int Seed { get; private set; }

        private static void CopyDeck(IReadOnlyList<CardDefinition> source, List<CardDefinition> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                CardDefinition card = source[i];
                if (card != null)
                {
                    target.Add(card);
                }
            }
        }
    }
}
