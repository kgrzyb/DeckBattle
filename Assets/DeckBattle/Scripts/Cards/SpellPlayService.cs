using System;

namespace DeckBattle
{
    public static class SpellPlayService
    {
        public static PlaySpellResult PlaySpell(BattleState battleState, PlayerBattleState player, CardRuntimeState card, SpellTarget target)
        {
            PlaySpellFailReason failReason = ValidatePlay(battleState, player, card, target);
            if (failReason != PlaySpellFailReason.None)
            {
                return PlaySpellResult.Failed(failReason);
            }

            SpellDefinition definition = card.SpellDefinition;
            RuntimeUnit targetUnit = target.Unit;
            HandService.RemoveFromHand(player, card);
            player.Ap -= definition.ApCost;
            card.Location = CardLocation.Played;
            player.PlayedCards.Add(card);

            int amount = Math.Max(0, definition.Amount);
            if (definition.EffectKind == SpellEffectKind.BuffAttackNextCombat)
            {
                ApplyBuffAttackNextCombat(targetUnit, amount);
            }

            return PlaySpellResult.Succeeded(targetUnit, amount);
        }

        public static PlaySpellFailReason ValidatePlay(BattleState battleState, PlayerBattleState player, CardRuntimeState card, SpellTarget target)
        {
            if (battleState == null)
            {
                throw new ArgumentNullException(nameof(battleState));
            }

            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            SpellDefinition definition = card != null ? card.SpellDefinition : null;
            if (definition == null)
            {
                return PlaySpellFailReason.InvalidCardType;
            }

            if (battleState.Phase != BattlePhase.Preparation)
            {
                return PlaySpellFailReason.NotInPreparation;
            }

            if (player.IsReady)
            {
                return PlaySpellFailReason.PlayerReady;
            }

            if (!HandService.IsInHand(player, card))
            {
                return card != null && card.Location == CardLocation.Played ? PlaySpellFailReason.SpellAlreadyPlayed : PlaySpellFailReason.CardNotInHand;
            }

            if (!HandService.CanPayForCard(player, card))
            {
                return PlaySpellFailReason.NotEnoughAp;
            }

            if (!IsEffectCompatibleWithTargeting(definition))
            {
                return PlaySpellFailReason.UnsupportedEffect;
            }

            if (!IsValidTarget(player, definition, target))
            {
                return PlaySpellFailReason.InvalidTarget;
            }

            if (definition.EffectKind != SpellEffectKind.BuffAttackNextCombat
                && definition.EffectKind != SpellEffectKind.None)
            {
                return PlaySpellFailReason.UnsupportedEffect;
            }

            return PlaySpellFailReason.None;
        }

        private static bool IsEffectCompatibleWithTargeting(SpellDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            if (definition.EffectKind == SpellEffectKind.BuffAttackNextCombat)
            {
                return definition.TargetingKind == SpellTargetingKind.FriendlyUnit;
            }

            if (definition.EffectKind == SpellEffectKind.None)
            {
                return definition.TargetingKind == SpellTargetingKind.None;
            }

            return false;
        }

        private static bool IsValidTarget(PlayerBattleState player, SpellDefinition definition, SpellTarget target)
        {
            if (definition == null)
            {
                return false;
            }

            if (definition.TargetingKind == SpellTargetingKind.None)
            {
                return target.Unit == null;
            }

            if (definition.TargetingKind == SpellTargetingKind.FriendlyUnit)
            {
                if (target.Unit == null || !target.Unit.IsAlive)
                {
                    return false;
                }

                return ContainsUnit(player, target.Unit);
            }

            return false;
        }

        private static bool ContainsUnit(PlayerBattleState player, RuntimeUnit targetUnit)
        {
            if (player == null)
            {
                return false;
            }

            for (int i = 0; i < player.Units.Count; i++)
            {
                if (player.Units[i] == targetUnit)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyBuffAttackNextCombat(RuntimeUnit targetUnit, int amount)
        {
            targetUnit.AttackBonusNextCombat = Math.Max(0, targetUnit.AttackBonusNextCombat + amount);
        }
    }
}
