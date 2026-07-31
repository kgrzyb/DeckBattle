using System;

namespace DeckBattle
{
    public static class HealingResolver
    {
        public static int Resolve(UnitRuntimeState target, int amount, BattleEventQueue eventQueue = null)
        {
            if (target == null || !target.IsAlive || amount <= 0) return 0;
            float reduction = Math.Max(0f, Math.Min(1f, target.StatusSnapshot.HealingReduction));
            int healed = Math.Min(target.CombatSpec.MaxHp - target.CurrentHp, (int)Math.Floor(amount * (1f - reduction)));
            if (healed <= 0) return 0;
            target.CurrentHp += healed;
            eventQueue?.Enqueue(BattleEvent.UnitHealed(target.UnitId, healed, target.CurrentHp));
            return healed;
        }
    }
}
