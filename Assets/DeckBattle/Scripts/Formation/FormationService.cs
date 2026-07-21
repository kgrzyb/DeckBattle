using System;

namespace DeckBattle
{
    public static class FormationService
    {
        public static FormationMoveResult MoveUnit(BattleState battleState, PlayerBattleState player, RuntimeUnit unit, HexCoord targetCoord)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (battleState.Phase != BattlePhase.Preparation)
            {
                return FormationMoveResult.Failed(FormationMoveFailReason.NotInPreparation);
            }

            if (player.IsReady)
            {
                return FormationMoveResult.Failed(FormationMoveFailReason.PlayerReady);
            }

            if (unit == null || !player.Units.Contains(unit))
            {
                return FormationMoveResult.Failed(FormationMoveFailReason.UnitMissing);
            }

            if (!battleState.Board.IsDeploymentCoord(player.Side, targetCoord))
            {
                return FormationMoveResult.Failed(FormationMoveFailReason.InvalidTile);
            }

            RuntimeUnit occupyingUnit = FindUnitAtFormationCoord(player, targetCoord, unit);
            if (occupyingUnit != null)
            {
                HexCoord sourceCoord = unit.FormationCoord;
                unit.FormationCoord = targetCoord;
                unit.BattleCoord = targetCoord;
                occupyingUnit.FormationCoord = sourceCoord;
                occupyingUnit.BattleCoord = sourceCoord;
                return FormationMoveResult.SucceededWithSwap(occupyingUnit);
            }

            unit.FormationCoord = targetCoord;
            unit.BattleCoord = targetCoord;
            return FormationMoveResult.Succeeded();
        }

        public static bool IsOccupied(PlayerBattleState player, HexCoord coord, RuntimeUnit ignoredUnit)
        {
            return FindUnitAtFormationCoord(player, coord, ignoredUnit) != null;
        }

        public static RuntimeUnit FindUnitAtFormationCoord(PlayerBattleState player, HexCoord coord, RuntimeUnit ignoredUnit)
        {
            for (int i = 0; i < player.Units.Count; i++)
            {
                RuntimeUnit unit = player.Units[i];
                if (unit == ignoredUnit)
                {
                    continue;
                }

                if (unit.FormationCoord == coord)
                {
                    return unit;
                }
            }

            return null;
        }

        public static void RestoreFormationAndResetRoundHealth(PlayerBattleState player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            for (int i = 0; i < player.Units.Count; i++)
            {
                player.Units[i].ResetForRound();
            }
        }
    }
}
