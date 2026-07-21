namespace DeckBattle
{
    public static class SpellTargetingUtility
    {
        public static bool TryFindFriendlyUnitAtCoord(PlayerBattleState player, HexCoord coord, out RuntimeUnit unit)
        {
            unit = null;
            if (player == null)
            {
                return false;
            }

            for (int i = 0; i < player.Units.Count; i++)
            {
                RuntimeUnit candidate = player.Units[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                if (candidate.BattleCoord == coord)
                {
                    unit = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
