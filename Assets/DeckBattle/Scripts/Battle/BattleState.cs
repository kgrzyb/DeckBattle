using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleState
    {
        private int nextRuntimeCardId = 1;
        private int nextRuntimeUnitId = 1;

        public BattleConfig Config { get; private set; }
        public HexBoard Board { get; private set; }
        public PlayerBattleState Player { get; private set; }
        public PlayerBattleState Enemy { get; private set; }
        public BattlePhase Phase { get; set; }
        public BattleSide ActivePreparationSide { get; set; }
        public int RoundNumber { get; private set; }
        public bool PreparationCountdownActive { get; private set; }
        public float PreparationCountdownRemaining { get; private set; }

        private BattleState()
        {
        }

        public static BattleState Create(BattleConfig config, IList<UnitDefinition> playerDeck, IList<UnitDefinition> enemyDeck, int seed)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var state = new BattleState
            {
                Config = config,
                Board = new HexBoard(config.BoardWidth, config.BoardHeight, 1f),
                Player = new PlayerBattleState(BattleSide.Player, config.StartingPlayerHp, config.StartingAp, config.StartingDeploymentSlots),
                Enemy = new PlayerBattleState(BattleSide.Enemy, config.StartingEnemyHp, config.StartingAp, config.StartingDeploymentSlots),
                Phase = BattlePhase.Preparation,
                ActivePreparationSide = BattleSide.Player,
                RoundNumber = 1
            };

            var rng = new DeterministicRandom(seed);
            DeckService.CreateDeck(playerDeck, state.Player.Deck, ref state.nextRuntimeCardId);
            DeckService.CreateDeck(enemyDeck, state.Enemy.Deck, ref state.nextRuntimeCardId);
            DeckService.Shuffle(state.Player.Deck, rng);
            DeckService.Shuffle(state.Enemy.Deck, rng);
            DeckService.DrawCards(state.Player, config.StartingHandSize);
            DeckService.DrawCards(state.Enemy, config.StartingHandSize);
            PreparationTurnService.EnsureActiveSideCanAct(state);
            return state;
        }

        public PlayerBattleState GetPlayerState(BattleSide side)
        {
            return side == BattleSide.Player ? Player : Enemy;
        }

        public void StartNextRound()
        {
            if (Phase != BattlePhase.RoundResolution)
            {
                throw new InvalidOperationException("Next round can only start after round resolution.");
            }

            RoundNumber++;
            Phase = BattlePhase.RoundStart;
            ActivePreparationSide = BattleSide.Player;
            StopPreparationCountdown();

            PreparePlayerForNextRound(Player);
            PreparePlayerForNextRound(Enemy);

            Phase = BattlePhase.Preparation;
            PreparationTurnService.EnsureActiveSideCanAct(this);
        }

        public int AllocateRuntimeUnitId()
        {
            int id = nextRuntimeUnitId;
            nextRuntimeUnitId++;
            return id;
        }

        public void StartPreparationCountdown(float duration)
        {
            if (Phase != BattlePhase.Preparation)
            {
                throw new InvalidOperationException("Preparation countdown can only start during preparation.");
            }

            PreparationCountdownActive = duration > 0f;
            PreparationCountdownRemaining = duration > 0f ? duration : 0f;
        }

        public bool TickPreparationCountdown(float deltaTime)
        {
            if (!PreparationCountdownActive)
            {
                return false;
            }

            float safeDeltaTime = deltaTime > 0f ? deltaTime : 0f;
            PreparationCountdownRemaining -= safeDeltaTime;
            if (PreparationCountdownRemaining < 0f)
            {
                PreparationCountdownRemaining = 0f;
            }

            return PreparationCountdownRemaining <= 0f;
        }

        public void StopPreparationCountdown()
        {
            PreparationCountdownActive = false;
            PreparationCountdownRemaining = 0f;
        }

        public void CompletePreparationCountdown()
        {
            if (Phase != BattlePhase.Preparation)
            {
                return;
            }

            Player.IsReady = true;
            Enemy.IsReady = true;
            StopPreparationCountdown();
            Phase = BattlePhase.Combat;
        }

        private void PreparePlayerForNextRound(PlayerBattleState player)
        {
            player.IsReady = false;
            player.Ap = CalculateRoundAp();
            player.DeploymentSlots = CalculateDeploymentSlots();
            FormationService.RestoreFormationAndResetRoundHealth(player);
            DeckService.DrawCards(player, Config.DrawPerRound);
        }

        private int CalculateRoundAp()
        {
            int roundAp = Config.StartingAp + RoundNumber - 1;
            return Math.Min(Config.MaxAp, roundAp);
        }

        private int CalculateDeploymentSlots()
        {
            int slotIncreases = (RoundNumber - 1) / Config.DeploymentSlotIncreaseEveryRounds;
            int deploymentSlots = Config.StartingDeploymentSlots + slotIncreases;
            return Math.Min(Config.MaxDeploymentSlots, deploymentSlots);
        }
    }
}
