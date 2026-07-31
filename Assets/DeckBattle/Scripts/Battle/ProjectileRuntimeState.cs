using System;

namespace DeckBattle
{
    public sealed class ProjectileRuntimeState
    {
        public readonly int ProjectileId;
        public readonly int AttackerUnitId;
        public readonly int TargetUnitId;
        public readonly ProjectileCombatSpec CombatSpec;
        public readonly HexCoord FromHex;
        public HexCoord LastKnownTargetHex;
        public readonly double ImpactTime;
        public readonly float TravelDuration;
        public readonly int Damage;
        public readonly bool IsCritical;

        public ProjectileRuntimeState(
            int projectileId,
            int attackerUnitId,
            int targetUnitId,
            ProjectileCombatSpec combatSpec,
            HexCoord fromHex,
            HexCoord lastKnownTargetHex,
            float travelDuration,
            double impactTime,
            int damage,
            bool isCritical)
        {
            if (projectileId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileId));
            }

            ProjectileId = projectileId;
            AttackerUnitId = attackerUnitId;
            TargetUnitId = targetUnitId;
            if (!combatSpec.IsValid)
            {
                throw new ArgumentException("Projectile combat spec is invalid.", nameof(combatSpec));
            }

            CombatSpec = combatSpec;
            FromHex = fromHex;
            LastKnownTargetHex = lastKnownTargetHex;
            TravelDuration = Math.Max(0f, travelDuration);
            ImpactTime = impactTime;
            Damage = Math.Max(0, damage);
            IsCritical = isCritical;
        }
    }
}
