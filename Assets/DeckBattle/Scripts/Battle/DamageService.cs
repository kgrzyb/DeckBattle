using System;

namespace DeckBattle
{
    public static class DamageService
    {
        public static bool ApplyAttack(RuntimeUnit attacker, RuntimeUnit target)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!attacker.IsAlive || !target.IsAlive)
            {
                return false;
            }

            int damage = Math.Max(0, attacker.Definition.Attack);
            target.CurrentHp -= damage;
            if (target.CurrentHp <= 0)
            {
                target.IsDefeated = true;
            }

            return damage > 0;
        }
    }
}
