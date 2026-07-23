using System;
using System.Collections.Generic;

namespace DeckBattle
{
    [Serializable]
    public sealed class PlayerProfile
    {
        public const int CurrentSaveVersion = 1;

        public int SaveVersion = CurrentSaveVersion;
        public List<string> UnlockedCardIds = new List<string>(16);
        public List<string> ActiveDeckCardIds = new List<string>(8);
        public int Shards;
    }
}
