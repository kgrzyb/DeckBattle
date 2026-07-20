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
                Player = new PlayerBattleState(
                    BattleSide.Player,
                    config.StartingPlayerHp,
                    config.StartingAp,
                    config.StartingDeploymentSlots,
                    config.StartingRoundDamageBonus),
                Enemy = new PlayerBattleState(
                    BattleSide.Enemy,
                    config.StartingEnemyHp,
                    config.StartingAp,
                    config.StartingDeploymentSlots,
                    config.StartingRoundDamageBonus),
                Phase = BattlePhase.Preparation,
                ActivePreparationSide = BattleSide.Player,
                RoundNumber = 1
            };

            var rng = new DeterministicRandom(seed);
            DeckService.CreateDeck(playerDeck, state.Player.Deck, ref state.nextRuntimeCardId);
            DeckService.CreateDeck(enemyDeck, state.Enemy.Deck, ref state.nextRuntimeCardId);
            DeckService.Shuffle(state.Player.Deck, rng);
            DeckService.Shuffle(state.Enemy.Deck, rng);
            DeckService.DrawCards(state.Player, config.StartingHandSize, config.MaxHandSize);
            DeckService.DrawCards(state.Enemy, config.StartingHandSize, config.MaxHandSize);
            PreparationTurnService.EnsureActiveSideCanAct(state);
            return state;
        }

        public PlayerBattleState GetPlayerState(BattleSide side)
        {
            return side == BattleSide.Player ? Player : Enemy;
        }

        public void BeginRoundStart()
        {
            if (Phase != BattlePhase.Preparation)
            {
                throw new InvalidOperationException("Round start can only be entered before preparation.");
            }

            StopPreparationCountdown();
            Phase = BattlePhase.RoundStart;
        }

        public void BeginPreparationAfterRoundStart()
        {
            if (Phase != BattlePhase.RoundStart)
            {
                throw new InvalidOperationException("Preparation can only begin after round start.");
            }

            Phase = BattlePhase.Preparation;
            ActivePreparationSide = BattleSide.Player;
            PreparationTurnService.EnsureActiveSideCanAct(this);
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
            player.RoundDamageBonus = CalculateRoundDamageBonus();
            FormationService.RestoreFormationAndResetRoundHealth(player);
            DeckService.DrawCards(player, Config.DrawPerRound, Config.MaxHandSize);
        }

        private int CalculateRoundAp()
        {
            return CalculateProgressionValue(
                Config.StartingAp,
                Config.ApIncreasePerStep,
                Config.ApIncreaseEveryRounds,
                Config.MaxAp);
        }

        private int CalculateDeploymentSlots()
        {
            return CalculateProgressionValue(
                Config.StartingDeploymentSlots,
                Config.DeploymentSlotIncreasePerStep,
                Config.DeploymentSlotIncreaseEveryRounds,
                Config.MaxDeploymentSlots);
        }

        private int CalculateRoundDamageBonus()
        {
            return CalculateProgressionValue(
                Config.StartingRoundDamageBonus,
                Config.RoundDamageBonusIncreasePerStep,
                Config.RoundDamageBonusIncreaseEveryRounds,
                Config.MaxRoundDamageBonus);
        }

        private int CalculateProgressionValue(int startingValue, int increasePerStep, int increaseEveryRounds, int maxValue)
        {
            int safeStartingValue = Math.Max(0, startingValue);
            int safeIncreaseEveryRounds = Math.Max(1, increaseEveryRounds);
            int safeMaxValue = Math.Max(safeStartingValue, maxValue);
            int steps = (RoundNumber - 1) / safeIncreaseEveryRounds;
            int value = safeStartingValue + steps * Math.Max(0, increasePerStep);
            return Math.Min(safeMaxValue, value);
        }
    }
}
