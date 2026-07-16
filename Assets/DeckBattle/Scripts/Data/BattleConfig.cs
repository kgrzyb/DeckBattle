using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "Deck Battle/Battle Config")]
    public sealed class BattleConfig : ScriptableObject
    {
        public int StartingPlayerHp = 30;
        public int StartingEnemyHp = 30;
        public int StartingAp = 3;
        public int MaxAp = 8;
        public int StartingHandSize = 3;
        public int DrawPerRound = 2;
        public int StartingDeploymentSlots = 3;
        public int MaxDeploymentSlots = 7;
        public int DeploymentSlotIncreaseEveryRounds = 2;
        public int BoardWidth = 5;
        public int BoardHeight = 6;
        public float PreparationCountdownSeconds = 10f;

        private void OnValidate()
        {
            StartingPlayerHp = Mathf.Max(1, StartingPlayerHp);
            StartingEnemyHp = Mathf.Max(1, StartingEnemyHp);
            StartingAp = Mathf.Max(0, StartingAp);
            MaxAp = Mathf.Max(StartingAp, MaxAp);
            StartingHandSize = Mathf.Max(0, StartingHandSize);
            DrawPerRound = Mathf.Max(0, DrawPerRound);
            StartingDeploymentSlots = Mathf.Max(0, StartingDeploymentSlots);
            MaxDeploymentSlots = Mathf.Max(StartingDeploymentSlots, MaxDeploymentSlots);
            DeploymentSlotIncreaseEveryRounds = Mathf.Max(1, DeploymentSlotIncreaseEveryRounds);
            BoardWidth = Mathf.Max(1, BoardWidth);
            BoardHeight = Mathf.Max(2, BoardHeight);
            PreparationCountdownSeconds = Mathf.Max(0f, PreparationCountdownSeconds);
        }
    }
}
