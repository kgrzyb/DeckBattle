using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "Deck Battle/Battle Config")]
    public sealed class BattleConfig : ScriptableObject
    {
        public int StartingPlayerHp = 30;
        public int StartingEnemyHp = 30;
        public int StartingAp = 3;
        public int ApIncreasePerStep = 1;
        public int ApIncreaseEveryRounds = 1;
        public int MaxAp = 8;
        public int StartingHandSize = 3;
        public int MaxHandSize = 7;
        public int DrawPerRound = 2;
        public int StartingDeploymentSlots = 3;
        public int DeploymentSlotIncreasePerStep = 1;
        public int MaxDeploymentSlots = 7;
        public int DeploymentSlotIncreaseEveryRounds = 2;
        public int StartingRoundDamageBonus = 0;
        public int RoundDamageBonusIncreasePerStep = 0;
        public int RoundDamageBonusIncreaseEveryRounds = 1;
        public int MaxRoundDamageBonus = 0;
        public int BoardWidth = 5;
        public int BoardHeight = 6;
        public float PreparationCountdownSeconds = 10f;

        private void OnValidate()
        {
            StartingPlayerHp = Mathf.Max(1, StartingPlayerHp);
            StartingEnemyHp = Mathf.Max(1, StartingEnemyHp);
            StartingAp = Mathf.Max(0, StartingAp);
            ApIncreasePerStep = Mathf.Max(0, ApIncreasePerStep);
            ApIncreaseEveryRounds = Mathf.Max(1, ApIncreaseEveryRounds);
            MaxAp = Mathf.Max(StartingAp, MaxAp);
            StartingHandSize = Mathf.Max(0, StartingHandSize);
            MaxHandSize = Mathf.Max(0, MaxHandSize);
            StartingHandSize = Mathf.Min(StartingHandSize, MaxHandSize);
            DrawPerRound = Mathf.Max(0, DrawPerRound);
            StartingDeploymentSlots = Mathf.Max(0, StartingDeploymentSlots);
            DeploymentSlotIncreasePerStep = Mathf.Max(0, DeploymentSlotIncreasePerStep);
            MaxDeploymentSlots = Mathf.Max(StartingDeploymentSlots, MaxDeploymentSlots);
            DeploymentSlotIncreaseEveryRounds = Mathf.Max(1, DeploymentSlotIncreaseEveryRounds);
            StartingRoundDamageBonus = Mathf.Max(0, StartingRoundDamageBonus);
            RoundDamageBonusIncreasePerStep = Mathf.Max(0, RoundDamageBonusIncreasePerStep);
            RoundDamageBonusIncreaseEveryRounds = Mathf.Max(1, RoundDamageBonusIncreaseEveryRounds);
            MaxRoundDamageBonus = Mathf.Max(StartingRoundDamageBonus, MaxRoundDamageBonus);
            BoardWidth = Mathf.Max(1, BoardWidth);
            BoardHeight = Mathf.Max(2, BoardHeight);
            PreparationCountdownSeconds = Mathf.Max(0f, PreparationCountdownSeconds);
        }
    }
}
