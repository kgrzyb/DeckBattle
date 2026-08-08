using System;

namespace DeckBattle
{
    public static class DamageCalculator
    {
        public static int CalculateDamage(UnitRuntimeState attacker, UnitRuntimeState target, int attackBonus, BattleRuntimeTuning tuning, DeterministicRandom rng, out bool isCritical)
        {
            return CalculateRuntimeDamage(
                attacker,
                target,
                attackBonus,
                1f,
                true,
                tuning,
                rng,
                out isCritical);
        }

        public static int CalculateSpecialDamage(
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            float attackDamageMultiplier,
            BattleRuntimeTuning tuning)
        {
            return CalculateRuntimeDamage(
                attacker,
                target,
                0,
                attackDamageMultiplier,
                false,
                tuning,
                null,
                out _);
        }

        private static int CalculateRuntimeDamage(
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            int attackBonus,
            float attackDamageMultiplier,
            bool canCritical,
            BattleRuntimeTuning tuning,
            DeterministicRandom rng,
            out bool isCritical)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (target == null) throw new ArgumentNullException(nameof(target));

            isCritical = canCritical && RollCritical(attacker.CombatSpec, rng);
            float armorPenetration = ClampPercentage(attacker.CombatSpec.ArmorPenetration);
            float effectiveArmor = ClampPercentage(target.CombatSpec.Armor) * (1f - armorPenetration / 100f);
            float damage = CalculateBaseDamageBeforeMitigation(
                attacker.CombatSpec.Attack,
                EffectiveStatsResolver.GetBaseAttackMultiplier(attacker, tuning),
                attackDamageMultiplier);
            damage += Math.Max(0, attackBonus);
            damage *= EffectiveStatsResolver.GetOutgoingDamageMultiplier(attacker, tuning);
            damage *= 1f - effectiveArmor / 100f;
            if (isCritical) damage *= EffectiveStatsResolver.GetCriticalMultiplier(attacker);
            return RoundDamage(damage);
        }

        public static int CalculateDamage(
            UnitDefinition attacker,
            UnitDefinition target,
            DeterministicRandom rng,
            out bool isCritical)
        {
            return CalculateDamage(attacker, target, 0, rng, out isCritical);
        }

        public static int CalculateDamage(
            UnitDefinition attacker,
            UnitDefinition target,
            int attackBonus,
            DeterministicRandom rng,
            out bool isCritical)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            isCritical = RollCritical(UnitCombatSpec.FromDefinition(attacker), rng);
            float armorPenetration = ClampPercentage(attacker.ArmorPenetration);
            float effectiveArmor = ClampPercentage(target.Armor) * (1f - armorPenetration / 100f);
            float damageAfterArmor = Math.Max(0, attacker.Attack + Math.Max(0, attackBonus)) * (1f - effectiveArmor / 100f);
            if (isCritical)
            {
                damageAfterArmor *= Math.Max(1f, attacker.CritMultiplier);
            }

            return RoundDamage(damageAfterArmor);
        }

        public static int CalculateBaseDamagePreview(int attack, float attackDamageMultiplier = 1f)
        {
            return RoundDamage(CalculateBaseDamageBeforeMitigation(attack, 1f, attackDamageMultiplier));
        }

        public static int CalculateBaseAttackBonusPreview(int attack, float bonusPercent)
        {
            int attackAfterBonus = CalculateBaseDamagePreview(attack, 1f + Math.Max(0f, bonusPercent));
            return Math.Max(0, attackAfterBonus - Math.Max(0, attack));
        }

        private static float CalculateBaseDamageBeforeMitigation(
            int attack,
            float baseAttackMultiplier,
            float attackDamageMultiplier)
        {
            return Math.Max(0, attack)
                * Math.Max(0f, baseAttackMultiplier)
                * Math.Max(0f, attackDamageMultiplier);
        }

        private static int RoundDamage(float value)
        {
            return Math.Max(0, (int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static bool RollCritical(UnitCombatSpec attacker, DeterministicRandom rng)
        {
            float critChance = ClampPercentage(attacker.CritChance);
            if (critChance <= 0f)
            {
                return false;
            }

            if (critChance >= 100f)
            {
                return true;
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            return rng.NextFloat01() * 100f < critChance;
        }

        private static float ClampPercentage(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 100f ? 100f : value;
        }
    }
}
