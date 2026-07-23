using System;

namespace DeckBattle
{
    [Serializable]
    public struct DeckBuilderRules
    {
        public int MinDeckSize;
        public int MaxDeckSize;

        public DeckBuilderRules(int minDeckSize, int maxDeckSize)
        {
            MinDeckSize = minDeckSize;
            MaxDeckSize = maxDeckSize;
        }

        public static DeckBuilderRules MvpDefault
        {
            get { return new DeckBuilderRules(1, 8); }
        }

        public int NormalizedMinDeckSize
        {
            get { return MinDeckSize < 0 ? 0 : MinDeckSize; }
        }

        public int NormalizedMaxDeckSize
        {
            get
            {
                int minDeckSize = NormalizedMinDeckSize;
                return MaxDeckSize < minDeckSize ? minDeckSize : MaxDeckSize;
            }
        }
    }
}
