using System;

namespace DeckBattle
{
    public static class HitResolver
    {
        public static HitResolutionResult ResolveHit(BattleSimulation simulation, UnitRuntimeState attacker, UnitRuntimeState target, int damage, bool isCritical, BattleEventQueue eventQueue)
        {
            return DamageResolver.Resolve(simulation, target, new DamageRequest(attacker, damage, DamageKind.Direct, isCritical), eventQueue);
        }
    }

    public readonly struct HitResolutionResult
    {
        public readonly bool DidHit;
        public readonly int Damage;
        public readonly bool Died;

        public HitResolutionResult(bool didHit, int damage, bool died)
        {
            DidHit = didHit;
            Damage = damage;
            Died = died;
        }
    }
}
