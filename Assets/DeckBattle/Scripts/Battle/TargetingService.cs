using System;

namespace DeckBattle
{
    public static class TargetingService
    {
        public static RuntimeUnit FindNearestTarget(HexBoard board, RuntimeUnit attacker, PlayerBattleState opponent)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (opponent == null)
            {
                throw new ArgumentNullException(nameof(opponent));
            }

            RuntimeUnit selected = null;
            int selectedDistance = int.MaxValue;
            int selectedHp = int.MaxValue;
            int selectedRuntimeId = int.MaxValue;

            for (int i = 0; i < opponent.Units.Count; i++)
            {
                RuntimeUnit candidate = opponent.Units[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                int distance = board.Distance(attacker.BattleCoord, candidate.BattleCoord);
                if (!IsBetterTarget(candidate, distance, selectedDistance, selectedHp, selectedRuntimeId))
                {
                    continue;
                }

                selected = candidate;
                selectedDistance = distance;
                selectedHp = candidate.CurrentHp;
                selectedRuntimeId = candidate.RuntimeId;
            }

            return selected;
        }

        private static bool IsBetterTarget(RuntimeUnit candidate, int distance, int selectedDistance, int selectedHp, int selectedRuntimeId)
        {
            if (distance != selectedDistance)
            {
                return distance < selectedDistance;
            }

            if (candidate.CurrentHp != selectedHp)
            {
                return candidate.CurrentHp < selectedHp;
            }

            return candidate.RuntimeId < selectedRuntimeId;
        }
    }
}
