namespace DeckBattle
{
    public static class BattleSession
    {
        public static BattleStartData PendingStartData { get; set; }

        public static bool TryConsumePendingStartData(out BattleStartData startData)
        {
            startData = PendingStartData;
            PendingStartData = null;
            return startData != null;
        }

        public static void Clear()
        {
            PendingStartData = null;
        }
    }
}
