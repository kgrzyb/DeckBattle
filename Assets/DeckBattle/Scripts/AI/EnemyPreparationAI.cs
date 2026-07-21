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
            int playedSpellCount = 0;
            RuntimeUnit lastPlayedUnit = null;
            RuntimeUnit lastSpellTargetUnit = null;
            EnemyPreparationAIPlay selectedPlay;
            while (TryFindPlay(battleState, out selectedPlay))
            {
                if (selectedPlay.CardKind == CardKind.Unit)
                {
                    PlayUnitResult playResult = UnitPlayService.PlayUnit(battleState, battleState.Enemy, selectedPlay.Card, selectedPlay.UnitCoord);
                    if (!playResult.Success)
                    {
                        break;
                    }

                    playedUnitCount++;
                    lastPlayedUnit = playResult.Unit;
                    continue;
                }

                PlaySpellResult spellResult = SpellPlayService.PlaySpell(battleState, battleState.Enemy, selectedPlay.Card, selectedPlay.SpellTarget);
                if (!spellResult.Success)
                {
                    break;
                }

                playedSpellCount++;
                lastSpellTargetUnit = spellResult.TargetUnit;
            }

            PreparationTurnService.MarkEnemyReady(battleState);
            return EnemyPreparationAIResult.Prepared(playedUnitCount, playedSpellCount, lastPlayedUnit, lastSpellTargetUnit);
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

            EnemyPreparationAIPlay selectedPlay;
            if (TryFindPlay(battleState, out selectedPlay))
            {
                if (selectedPlay.CardKind == CardKind.Unit)
                {
                    PlayUnitResult playResult = UnitPlayService.PlayUnit(battleState, battleState.Enemy, selectedPlay.Card, selectedPlay.UnitCoord);
                    if (!playResult.Success)
                    {
                        return EnemyPreparationAIResult.NoAction();
                    }

                    return EnemyPreparationAIResult.PlayedUnitCard(playResult.Unit);
                }

                PlaySpellResult spellResult = SpellPlayService.PlaySpell(battleState, battleState.Enemy, selectedPlay.Card, selectedPlay.SpellTarget);
                if (!spellResult.Success)
                {
                    return EnemyPreparationAIResult.NoAction();
                }

                return EnemyPreparationAIResult.PlayedSpellCard(spellResult.TargetUnit);
            }

            PreparationTurnService.MarkEnemyReady(battleState);
            return EnemyPreparationAIResult.Passed();
        }

        private static bool TryFindPlay(BattleState battleState, out EnemyPreparationAIPlay selectedPlay)
        {
            if (TryFindUnitPlay(battleState, out selectedPlay))
            {
                return true;
            }

            return TryFindSpellPlay(battleState, out selectedPlay);
        }

        private static bool TryFindUnitPlay(BattleState battleState, out EnemyPreparationAIPlay selectedPlay)
        {
            PlayerBattleState enemy = battleState.Enemy;
            for (int cardIndex = 0; cardIndex < enemy.Hand.Count; cardIndex++)
            {
                CardRuntimeState card = enemy.Hand[cardIndex];
                if (card == null || card.UnitDefinition == null)
                {
                    continue;
                }

                HexCoord selectedCoord;
                if (TryFindCoordForCard(battleState, card, out selectedCoord))
                {
                    selectedPlay = EnemyPreparationAIPlay.ForUnit(card, selectedCoord);
                    return true;
                }
            }

            selectedPlay = default(EnemyPreparationAIPlay);
            return false;
        }

        private static bool TryFindSpellPlay(BattleState battleState, out EnemyPreparationAIPlay selectedPlay)
        {
            PlayerBattleState enemy = battleState.Enemy;
            for (int cardIndex = 0; cardIndex < enemy.Hand.Count; cardIndex++)
            {
                CardRuntimeState card = enemy.Hand[cardIndex];
                if (card == null || card.SpellDefinition == null)
                {
                    continue;
                }

                SpellTarget target;
                if (TryFindTargetForSpell(battleState, card, out target))
                {
                    selectedPlay = EnemyPreparationAIPlay.ForSpell(card, target);
                    return true;
                }
            }

            selectedPlay = default(EnemyPreparationAIPlay);
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

        private static bool TryFindTargetForSpell(BattleState battleState, CardRuntimeState card, out SpellTarget target)
        {
            SpellDefinition spellDefinition = card != null ? card.SpellDefinition : null;
            if (spellDefinition == null)
            {
                target = SpellTarget.None();
                return false;
            }

            if (spellDefinition.TargetingKind == SpellTargetingKind.None)
            {
                target = SpellTarget.None();
                return SpellPlayService.ValidatePlay(battleState, battleState.Enemy, card, target) == PlaySpellFailReason.None;
            }

            if (spellDefinition.TargetingKind == SpellTargetingKind.FriendlyUnit)
            {
                RuntimeUnit targetUnit;
                if (TryFindFriendlySpellTarget(battleState.Enemy, out targetUnit))
                {
                    target = SpellTarget.ForUnit(targetUnit);
                    return SpellPlayService.ValidatePlay(battleState, battleState.Enemy, card, target) == PlaySpellFailReason.None;
                }
            }

            target = SpellTarget.None();
            return false;
        }

        private static bool TryFindFriendlySpellTarget(PlayerBattleState enemy, out RuntimeUnit targetUnit)
        {
            targetUnit = null;
            if (enemy == null)
            {
                return false;
            }

            for (int i = 0; i < enemy.Units.Count; i++)
            {
                RuntimeUnit candidate = enemy.Units[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                if (targetUnit == null || candidate.Definition.Attack > targetUnit.Definition.Attack)
                {
                    targetUnit = candidate;
                }
            }

            return targetUnit != null;
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

    internal readonly struct EnemyPreparationAIPlay
    {
        public readonly CardRuntimeState Card;
        public readonly CardKind CardKind;
        public readonly HexCoord UnitCoord;
        public readonly SpellTarget SpellTarget;

        private EnemyPreparationAIPlay(CardRuntimeState card, CardKind cardKind, HexCoord unitCoord, SpellTarget spellTarget)
        {
            Card = card;
            CardKind = cardKind;
            UnitCoord = unitCoord;
            SpellTarget = spellTarget;
        }

        public static EnemyPreparationAIPlay ForUnit(CardRuntimeState card, HexCoord coord)
        {
            return new EnemyPreparationAIPlay(card, CardKind.Unit, coord, SpellTarget.None());
        }

        public static EnemyPreparationAIPlay ForSpell(CardRuntimeState card, SpellTarget target)
        {
            return new EnemyPreparationAIPlay(card, CardKind.Spell, default(HexCoord), target);
        }
    }

    public sealed class EnemyPreparationAIResult
    {
        public readonly bool PlayedUnit;
        public readonly bool PlayedSpell;
        public readonly bool MarkedReady;
        public readonly int PlayedCardCount;
        public readonly int PlayedUnitCount;
        public readonly int PlayedSpellCount;
        public readonly RuntimeUnit Unit;
        public readonly RuntimeUnit SpellTargetUnit;

        private EnemyPreparationAIResult(
            bool playedUnit,
            bool playedSpell,
            bool markedReady,
            int playedUnitCount,
            int playedSpellCount,
            RuntimeUnit unit,
            RuntimeUnit spellTargetUnit)
        {
            PlayedUnit = playedUnit;
            PlayedSpell = playedSpell;
            MarkedReady = markedReady;
            PlayedCardCount = playedUnitCount + playedSpellCount;
            PlayedUnitCount = playedUnitCount;
            PlayedSpellCount = playedSpellCount;
            Unit = unit;
            SpellTargetUnit = spellTargetUnit;
        }

        public static EnemyPreparationAIResult Prepared(int playedUnitCount, int playedSpellCount, RuntimeUnit lastUnit, RuntimeUnit lastSpellTargetUnit)
        {
            return new EnemyPreparationAIResult(playedUnitCount > 0, playedSpellCount > 0, true, playedUnitCount, playedSpellCount, lastUnit, lastSpellTargetUnit);
        }

        public static EnemyPreparationAIResult PlayedUnitCard(RuntimeUnit unit)
        {
            return new EnemyPreparationAIResult(true, false, false, 1, 0, unit, null);
        }

        public static EnemyPreparationAIResult PlayedSpellCard(RuntimeUnit targetUnit)
        {
            return new EnemyPreparationAIResult(false, true, false, 0, 1, null, targetUnit);
        }

        public static EnemyPreparationAIResult Passed()
        {
            return new EnemyPreparationAIResult(false, false, true, 0, 0, null, null);
        }

        public static EnemyPreparationAIResult NoAction()
        {
            return new EnemyPreparationAIResult(false, false, false, 0, 0, null, null);
        }
    }
}
