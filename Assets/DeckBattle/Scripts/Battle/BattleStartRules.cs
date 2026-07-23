using System;

namespace DeckBattle
{
    [Serializable]
    public struct BattleStartRules
    {
        public int MinDeckSize;
        public int MaxDeckSize;
        public bool UsePlayerDeckWhenEnemyDeckMissing;

        public BattleStartRules(int minDeckSize, int maxDeckSize)
        {
            MinDeckSize = minDeckSize;
            MaxDeckSize = maxDeckSize;
            UsePlayerDeckWhenEnemyDeckMissing = true;
        }

        public static BattleStartRules MvpDefault
        {
            get { return new BattleStartRules(DeckBuilderRules.MvpDefault.MinDeckSize, DeckBuilderRules.MvpDefault.MaxDeckSize); }
        }

        public DeckBuilderRules DeckRules
        {
            get { return new DeckBuilderRules(MinDeckSize, MaxDeckSize); }
        }
    }
}
