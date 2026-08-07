using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleState
    {
        private const int PreparationOrderSeedSalt = 51696588;

        private int nextRuntimeCardId = 1;
        private int nextRuntimeUnitId = 1;

        public BattleConfig Config { get; private set; }
        public HexBoard Board { get; private set; }
        public PlayerBattleState Player { get; private set; }
        public PlayerBattleState Enemy { get; private set; }
        public BattlePhase Phase { get; set; }
        public BattleSide InitialPreparationSide { get; private set; }
        public BattleSide ActivePreparationSide { get; internal set; }
        public int RoundNumber { get; private set; }
        public PendingCombatEffectQueue PendingCombatEffects { get; private set; }

        private BattleState()
        {
        }

        public static BattleState Create(BattleConfig config, IReadOnlyList<CardDefinition> playerDeck, IReadOnlyList<CardDefinition> enemyDeck, int seed)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            BattleSide initialPreparationSide = ResolveInitialPreparationSide(seed);
            var state = new BattleState
            {
                Config = config,
                Board = new HexBoard(config.BoardWidth, config.BoardHeight, 1f),
                Player = new PlayerBattleState(
                    BattleSide.Player,
                    config.StartingPlayerHp,
                    config.StartingAp,
                    config.StartingRoundDamageBonus),
                Enemy = new PlayerBattleState(
                    BattleSide.Enemy,
                    config.StartingEnemyHp,
                    config.StartingAp,
                    config.StartingRoundDamageBonus),
                Phase = BattlePhase.Preparation,
                InitialPreparationSide = initialPreparationSide,
                ActivePreparationSide = initialPreparationSide,
                RoundNumber = 1,
                PendingCombatEffects = new PendingCombatEffectQueue(config.MaxPendingCombatEffects)
            };

            var rng = new DeterministicRandom(seed);
            DeckService.CreateDeck(playerDeck, state.Player.Deck, ref state.nextRuntimeCardId);
            DeckService.CreateDeck(enemyDeck, state.Enemy.Deck, ref state.nextRuntimeCardId);
            DeckService.Shuffle(state.Player.Deck, rng);
            DeckService.Shuffle(state.Enemy.Deck, rng);
            DeckService.DrawCards(state.Player, config.StartingHandSize, config.MaxHandSize);
            DeckService.DrawCards(state.Enemy, config.StartingHandSize, config.MaxHandSize);
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

            Phase = BattlePhase.RoundStart;
        }

        public void BeginPreparationAfterRoundStart()
        {
            if (Phase != BattlePhase.RoundStart)
            {
                throw new InvalidOperationException("Preparation can only begin after round start.");
            }

            Phase = BattlePhase.Preparation;
            ActivePreparationSide = GetPreparationStarterForRound();
        }

        public void StartNextRound()
        {
            if (Phase != BattlePhase.RoundResolution)
            {
                throw new InvalidOperationException("Next round can only start after round resolution.");
            }

            RoundNumber++;
            Phase = BattlePhase.RoundStart;
            ActivePreparationSide = GetPreparationStarterForRound();
            PendingCombatEffects.RemoveBeforeRound(RoundNumber);

            PreparePlayerForNextRound(Player);
            PreparePlayerForNextRound(Enemy);
        }

        public int AllocateRuntimeUnitId()
        {
            int id = nextRuntimeUnitId;
            nextRuntimeUnitId++;
            return id;
        }

        internal static BattleSide GetOppositeSide(BattleSide side)
        {
            return side == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;
        }

        private static BattleSide ResolveInitialPreparationSide(int seed)
        {
            var preparationOrderRandom = new DeterministicRandom(seed ^ PreparationOrderSeedSalt);
            return preparationOrderRandom.NextInt(0, 2) == 0 ? BattleSide.Player : BattleSide.Enemy;
        }

        private BattleSide GetPreparationStarterForRound()
        {
            return (RoundNumber & 1) == 1
                ? InitialPreparationSide
                : GetOppositeSide(InitialPreparationSide);
        }

        private void PreparePlayerForNextRound(PlayerBattleState player)
        {
            player.IsReady = false;
            player.Ap = CalculateRoundAp();
            player.RoundDamageBonus = CalculateRoundDamageBonus();
            FormationService.RestoreFormationAndResetRoundHealth(player);
            DeckService.DrawCards(player, Config.DrawPerRound, Config.MaxHandSize);
        }

        private int CalculateRoundAp()
        {
            long startingAp = Math.Max(0, Config.StartingAp);
            long apIncreasePerRound = Math.Max(0, Config.ApIncreasePerRound);
            long value = startingAp + (RoundNumber - 1L) * apIncreasePerRound;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
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
