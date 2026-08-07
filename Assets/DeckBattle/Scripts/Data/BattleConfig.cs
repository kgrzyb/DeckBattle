using UnityEngine;
using UnityEngine.Serialization;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "Deck Battle/Battle Config")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Header("Realtime Combat")]
        public BattleRuntimeTuningConfig RuntimeTuningConfig;

        [Header("Match Setup")]
        public int StartingPlayerHp = 30;
        public int StartingEnemyHp = 30;
        public int StartingAp = 1;
        [FormerlySerializedAs("ApIncreasePerStep")]
        public int ApIncreasePerRound = 1;
        public int StartingHandSize = 3;
        public int MaxHandSize = 5;
        public int DrawPerRound = 1;
        public int MaxUnitsPerSide = 8;
        public int StartingRoundDamageBonus = 0;
        public int RoundDamageBonusIncreasePerStep = 0;
        public int RoundDamageBonusIncreaseEveryRounds = 1;
        public int MaxRoundDamageBonus = 0;

        [Header("Board")]
        public int BoardWidth = 5;
        public int BoardHeight = 6;
        [Min(0.01f)] public float HexSize = 1f;

        [Header("Combat Limits")]
        [Min(1)] public int MaxPendingCombatEffects = 32;

        private void OnValidate()
        {
            StartingPlayerHp = Mathf.Max(1, StartingPlayerHp);
            StartingEnemyHp = Mathf.Max(1, StartingEnemyHp);
            StartingAp = Mathf.Max(0, StartingAp);
            ApIncreasePerRound = Mathf.Max(0, ApIncreasePerRound);
            StartingHandSize = Mathf.Max(0, StartingHandSize);
            MaxHandSize = Mathf.Max(0, MaxHandSize);
            StartingHandSize = Mathf.Min(StartingHandSize, MaxHandSize);
            DrawPerRound = Mathf.Max(0, DrawPerRound);
            MaxUnitsPerSide = Mathf.Max(1, MaxUnitsPerSide);
            StartingRoundDamageBonus = Mathf.Max(0, StartingRoundDamageBonus);
            RoundDamageBonusIncreasePerStep = Mathf.Max(0, RoundDamageBonusIncreasePerStep);
            RoundDamageBonusIncreaseEveryRounds = Mathf.Max(1, RoundDamageBonusIncreaseEveryRounds);
            MaxRoundDamageBonus = Mathf.Max(StartingRoundDamageBonus, MaxRoundDamageBonus);
            BoardWidth = Mathf.Max(1, BoardWidth);
            BoardHeight = Mathf.Max(2, BoardHeight);
            HexSize = Mathf.Max(0.01f, HexSize);
            MaxPendingCombatEffects = Mathf.Max(1, MaxPendingCombatEffects);
        }
    }
}
