using System;

namespace DeckBattle
{
    public enum OnPlayEffectValidationResult
    {
        Valid = 0,
        InvalidDefinition = 1,
        QueueCapacityReached = 2
    }

    public readonly struct OnPlayEffectResolutionResult
    {
        public readonly bool Success;
        public readonly int QueuedEffectCount;

        private OnPlayEffectResolutionResult(bool success, int queuedEffectCount)
        {
            Success = success;
            QueuedEffectCount = queuedEffectCount;
        }

        public static OnPlayEffectResolutionResult Succeeded(int queuedEffectCount)
        {
            return new OnPlayEffectResolutionResult(true, queuedEffectCount);
        }

        public static OnPlayEffectResolutionResult Failed()
        {
            return new OnPlayEffectResolutionResult(false, 0);
        }
    }

    public static class UnitOnPlayEffectResolver
    {
        public static OnPlayEffectValidationResult Validate(
            BattleState battleState,
            PlayerBattleState player,
            UnitDefinition definition,
            HexCoord spawnCoord)
        {
            return ValidateInternal(battleState, player, definition, spawnCoord, true);
        }

        private static OnPlayEffectValidationResult ValidateInternal(
            BattleState battleState,
            PlayerBattleState player,
            UnitDefinition definition,
            HexCoord spawnCoord,
            bool includeProspectiveSource)
        {
            if (battleState == null || player == null || definition == null)
            {
                return OnPlayEffectValidationResult.InvalidDefinition;
            }

            UnitOnPlayEffectDefinition effectDefinition = definition.OnPlayEffect;
            if (effectDefinition == null)
            {
                return OnPlayEffectValidationResult.Valid;
            }

            UnitEffectStepDefinition[] steps = effectDefinition.Steps;
            if (steps == null)
            {
                return OnPlayEffectValidationResult.InvalidDefinition;
            }

            int requiredCount = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                PendingCombatEffectSpec spec;
                if (!PendingCombatEffectSpec.TryCreate(steps[i].Effect, out spec)
                    || !spec.IsValid
                    || !IsSupportedTarget(steps[i].Target.Kind))
                {
                    return OnPlayEffectValidationResult.InvalidDefinition;
                }

                requiredCount += CountTargets(battleState, player.Side, spawnCoord, steps[i].Target, includeProspectiveSource);
            }

            return battleState.PendingCombatEffects.CanReserve(requiredCount)
                ? OnPlayEffectValidationResult.Valid
                : OnPlayEffectValidationResult.QueueCapacityReached;
        }

        public static OnPlayEffectResolutionResult Resolve(BattleState battleState, PlayerBattleState player, RuntimeUnit source)
        {
            if (battleState == null || player == null || source == null)
            {
                return OnPlayEffectResolutionResult.Failed();
            }

            UnitOnPlayEffectDefinition effectDefinition = source.Definition.OnPlayEffect;
            if (effectDefinition == null)
            {
                return OnPlayEffectResolutionResult.Succeeded(0);
            }

            if (ValidateInternal(battleState, player, source.Definition, source.FormationCoord, false) != OnPlayEffectValidationResult.Valid)
            {
                return OnPlayEffectResolutionResult.Failed();
            }

            int queueCountBefore = battleState.PendingCombatEffects.Count;
            int queuedEffectCount = 0;
            UnitEffectStepDefinition[] steps = effectDefinition.Steps;
            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                PendingCombatEffectSpec spec;
                if (!PendingCombatEffectSpec.TryCreate(steps[stepIndex].Effect, out spec)
                    || !TryEnqueueTargets(battleState, source, steps[stepIndex].Target, spec, ref queuedEffectCount))
                {
                    battleState.PendingCombatEffects.RollbackTo(queueCountBefore);
                    return OnPlayEffectResolutionResult.Failed();
                }
            }

            return OnPlayEffectResolutionResult.Succeeded(queuedEffectCount);
        }

        private static bool TryEnqueueTargets(
            BattleState battleState,
            RuntimeUnit source,
            EffectTargetDefinition targetDefinition,
            PendingCombatEffectSpec spec,
            ref int queuedEffectCount)
        {
            if (targetDefinition.Kind == EffectTargetKind.Self)
            {
                if (!battleState.PendingCombatEffects.TryEnqueue(
                        battleState.RoundNumber,
                        source.RuntimeId,
                        source.RuntimeId,
                        spec))
                {
                    return false;
                }

                queuedEffectCount++;
                return true;
            }

            int playerIndex = 0;
            int enemyIndex = 0;
            while (TryGetNextUnit(battleState, ref playerIndex, ref enemyIndex, out RuntimeUnit target))
            {
                if (!IsTarget(targetDefinition, source, target))
                {
                    continue;
                }

                if (!battleState.PendingCombatEffects.TryEnqueue(
                        battleState.RoundNumber,
                        source.RuntimeId,
                        target.RuntimeId,
                        spec))
                {
                    return false;
                }

                queuedEffectCount++;
            }

            return true;
        }

        private static int CountTargets(
            BattleState battleState,
            BattleSide sourceSide,
            HexCoord sourceCoord,
            EffectTargetDefinition targetDefinition,
            bool includeProspectiveSource)
        {
            if (targetDefinition.Kind == EffectTargetKind.Self)
            {
                return 1;
            }

            int count = 0;
            int playerIndex = 0;
            int enemyIndex = 0;
            while (TryGetNextUnit(battleState, ref playerIndex, ref enemyIndex, out RuntimeUnit target))
            {
                if (IsTarget(targetDefinition, sourceSide, sourceCoord, target.Side, target.FormationCoord))
                {
                    count++;
                }
            }

            if (includeProspectiveSource && IsTarget(targetDefinition, sourceSide, sourceCoord, sourceSide, sourceCoord))
            {
                count++;
            }

            return count;
        }

        private static bool IsTarget(EffectTargetDefinition targetDefinition, RuntimeUnit source, RuntimeUnit target)
        {
            return target != null
                && target.IsAlive
                && IsTarget(targetDefinition, source.Side, source.FormationCoord, target.Side, target.FormationCoord);
        }

        private static bool IsTarget(
            EffectTargetDefinition targetDefinition,
            BattleSide sourceSide,
            HexCoord sourceCoord,
            BattleSide targetSide,
            HexCoord targetCoord)
        {
            bool friendly = sourceSide == targetSide;
            switch (targetDefinition.Kind)
            {
                case EffectTargetKind.AllFriendlyUnits:
                    return friendly;
                case EffectTargetKind.AllEnemyUnits:
                    return !friendly;
                case EffectTargetKind.FriendlyUnitsInRadius:
                    return friendly && sourceCoord.DistanceTo(targetCoord) <= targetDefinition.Radius;
                case EffectTargetKind.EnemyUnitsInRadius:
                    return !friendly && sourceCoord.DistanceTo(targetCoord) <= targetDefinition.Radius;
                case EffectTargetKind.AllUnitsInRadius:
                    return sourceCoord.DistanceTo(targetCoord) <= targetDefinition.Radius;
                default:
                    return false;
            }
        }

        private static bool IsSupportedTarget(EffectTargetKind kind)
        {
            return kind == EffectTargetKind.Self
                || kind == EffectTargetKind.AllFriendlyUnits
                || kind == EffectTargetKind.AllEnemyUnits
                || kind == EffectTargetKind.FriendlyUnitsInRadius
                || kind == EffectTargetKind.EnemyUnitsInRadius
                || kind == EffectTargetKind.AllUnitsInRadius;
        }

        private static bool TryGetNextUnit(BattleState battleState, ref int playerIndex, ref int enemyIndex, out RuntimeUnit unit)
        {
            RuntimeUnit playerUnit = GetNextAliveUnit(battleState.Player, playerIndex, out int nextPlayerIndex);
            RuntimeUnit enemyUnit = GetNextAliveUnit(battleState.Enemy, enemyIndex, out int nextEnemyIndex);
            if (playerUnit == null && enemyUnit == null)
            {
                unit = null;
                return false;
            }

            if (enemyUnit == null || (playerUnit != null && playerUnit.RuntimeId < enemyUnit.RuntimeId))
            {
                playerIndex = nextPlayerIndex;
                unit = playerUnit;
                return true;
            }

            enemyIndex = nextEnemyIndex;
            unit = enemyUnit;
            return true;
        }

        private static RuntimeUnit GetNextAliveUnit(PlayerBattleState player, int startIndex, out int nextIndex)
        {
            for (int i = startIndex; i < player.Units.Count; i++)
            {
                RuntimeUnit unit = player.Units[i];
                if (unit != null && unit.IsAlive)
                {
                    nextIndex = i + 1;
                    return unit;
                }
            }

            nextIndex = player.Units.Count;
            return null;
        }
    }
}
