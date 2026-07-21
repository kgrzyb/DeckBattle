using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "BattleTimingConfig", menuName = "Deck Battle/Battle Timing Config")]
    public sealed class BattleTimingConfig : ScriptableObject
    {
        public float CombatTickDuration = BattleTiming.DefaultCombatTickDuration;
        public int MaxCombatTicks = BattleTiming.DefaultMaxCombatTicks;
        public int MaxTicksPerFrame = BattleTiming.DefaultMaxTicksPerFrame;
        public float RoundResolutionDelay = BattleTiming.DefaultRoundResolutionDelay;

        private void OnValidate()
        {
            CombatTickDuration = Mathf.Max(BattleTiming.MinCombatTickDuration, CombatTickDuration);
            MaxCombatTicks = Mathf.Max(1, MaxCombatTicks);
            MaxTicksPerFrame = Mathf.Max(1, MaxTicksPerFrame);
            RoundResolutionDelay = Mathf.Max(0f, RoundResolutionDelay);
        }
    }
}
