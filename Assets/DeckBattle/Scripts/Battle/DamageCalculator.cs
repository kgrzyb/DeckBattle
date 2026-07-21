using System;

namespace DeckBattle
{
    public static class DamageCalculator
    {
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

            isCritical = RollCritical(attacker, rng);
            float armorPenetration = ClampPercentage(attacker.ArmorPenetration);
            float effectiveArmor = ClampPercentage(target.Armor) * (1f - armorPenetration / 100f);
            float damageAfterArmor = Math.Max(0, attacker.Attack + Math.Max(0, attackBonus)) * (1f - effectiveArmor / 100f);
            if (isCritical)
            {
                damageAfterArmor *= Math.Max(1f, attacker.CritMultiplier);
            }

            return Math.Max(0, (int)Math.Round(damageAfterArmor, MidpointRounding.AwayFromZero));
        }

        private static bool RollCritical(UnitDefinition attacker, DeterministicRandom rng)
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
