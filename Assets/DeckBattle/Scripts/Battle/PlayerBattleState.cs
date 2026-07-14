using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class PlayerBattleState
    {
        public readonly BattleSide Side;
        public int Hp;
        public int Ap;
        public int DeploymentSlots;
        public readonly List<CardRuntimeState> Deck = new List<CardRuntimeState>(16);
        public readonly List<CardRuntimeState> Hand = new List<CardRuntimeState>(8);
        public readonly List<CardRuntimeState> PlayedCards = new List<CardRuntimeState>(8);
        public readonly List<RuntimeUnit> Units = new List<RuntimeUnit>(8);

        public PlayerBattleState(BattleSide side, int hp, int ap, int deploymentSlots)
        {
            Side = side;
            Hp = hp;
            Ap = ap;
            DeploymentSlots = deploymentSlots;
        }
    }
}
