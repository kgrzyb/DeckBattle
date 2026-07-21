using System;

namespace DeckBattle
{
    public static class EnemyPreparationAI
    {
        public static EnemyPreparationAIResult PrepareFormation(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (!PreparationTurnService.CanEnemyPrepare(battleState))
            {
                return EnemyPreparationAIResult.NoAction();
            }

            int playedUnitCount = 0;
            RuntimeUnit lastPlayedUnit = null;
            CardRuntimeState selectedCard;
            HexCoord selectedCoord;
            while (TryFindPlay(battleState, out selectedCard, out selectedCoord))
            {
                PlayUnitResult playResult = UnitPlayService.PlayUnit(battleState, battleState.Enemy, selectedCard, selectedCoord);
                if (!playResult.Success)
                {
                    break;
                }

                playedUnitCount++;
                lastPlayedUnit = playResult.Unit;
            }

            PreparationTurnService.MarkEnemyReady(battleState);
            return EnemyPreparationAIResult.Prepared(playedUnitCount, lastPlayedUnit);
        }

        public static EnemyPreparationAIResult ExecuteTurn(BattleState battleState)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (!PreparationTurnService.CanEnemyPrepare(battleState))
            {
                return EnemyPreparationAIResult.NoAction();
            }

            CardRuntimeState selectedCard;
            HexCoord selectedCoord;
            if (TryFindPlay(battleState, out selectedCard, out selectedCoord))
            {
                PlayUnitResult playResult = UnitPlayService.PlayUnit(battleState, battleState.Enemy, selectedCard, selectedCoord);
                if (!playResult.Success)
                {
                    return EnemyPreparationAIResult.NoAction();
                }

                return EnemyPreparationAIResult.Played(playResult.Unit);
            }

            PreparationTurnService.MarkEnemyReady(battleState);
            return EnemyPreparationAIResult.Passed();
        }

        private static bool TryFindPlay(BattleState battleState, out CardRuntimeState selectedCard, out HexCoord selectedCoord)
        {
            PlayerBattleState enemy = battleState.Enemy;
            for (int cardIndex = 0; cardIndex < enemy.Hand.Count; cardIndex++)
            {
                CardRuntimeState card = enemy.Hand[cardIndex];
                if (card == null || card.UnitDefinition == null)
                {
                    continue;
                }

                if (TryFindCoordForCard(battleState, card, out selectedCoord))
                {
                    selectedCard = card;
                    return true;
                }
            }

            selectedCard = null;
            selectedCoord = default(HexCoord);
            return false;
        }

        private static bool TryFindCoordForCard(BattleState battleState, CardRuntimeState card, out HexCoord selectedCoord)
        {
            UnitDefinition unitDefinition = card != null ? card.UnitDefinition : null;
            if (unitDefinition == null)
            {
                selectedCoord = default(HexCoord);
                return false;
            }

            int deploymentRows = battleState.Board.Height / 2;
            int minRow = battleState.Board.Height - deploymentRows;
            int maxRow = battleState.Board.Height - 1;

            bool frontToBack = unitDefinition.UnitType == UnitType.Melee;
            for (int rowStep = 0; rowStep < deploymentRows; rowStep++)
            {
                int row = frontToBack ? minRow + rowStep : maxRow - rowStep;
                for (int columnStep = 0; columnStep < battleState.Board.Width; columnStep++)
                {
                    int column = GetCenterFirstColumn(battleState.Board.Width, columnStep);
                    var coord = new HexCoord(column, row);
                    if (UnitPlayService.ValidatePlay(battleState, battleState.Enemy, card, coord) == PlayUnitFailReason.None)
                    {
                        selectedCoord = coord;
                        return true;
                    }
                }
            }

            selectedCoord = default(HexCoord);
            return false;
        }

        private static int GetCenterFirstColumn(int width, int step)
        {
            int center = width / 2;
            if (step == 0)
            {
                return center;
            }

            int offset = (step + 1) / 2;
            bool left = (step & 1) == 1;
            int column = left ? center - offset : center + offset;
            if (column < 0)
            {
                return center + offset;
            }

            if (column >= width)
            {
                return center - offset;
            }

            return column;
        }
    }

    public sealed class EnemyPreparationAIResult
    {
        public readonly bool PlayedUnit;
        public readonly bool MarkedReady;
        public readonly int PlayedUnitCount;
        public readonly RuntimeUnit Unit;

        private EnemyPreparationAIResult(bool playedUnit, bool markedReady, int playedUnitCount, RuntimeUnit unit)
        {
            PlayedUnit = playedUnit;
            MarkedReady = markedReady;
            PlayedUnitCount = playedUnitCount;
            Unit = unit;
        }

        public static EnemyPreparationAIResult Prepared(int playedUnitCount, RuntimeUnit lastUnit)
        {
            return new EnemyPreparationAIResult(playedUnitCount > 0, true, playedUnitCount, lastUnit);
        }

        public static EnemyPreparationAIResult Played(RuntimeUnit unit)
        {
            return new EnemyPreparationAIResult(true, false, 1, unit);
        }

        public static EnemyPreparationAIResult Passed()
        {
            return new EnemyPreparationAIResult(false, true, 0, null);
        }

        public static EnemyPreparationAIResult NoAction()
        {
            return new EnemyPreparationAIResult(false, false, 0, null);
        }
    }
}
