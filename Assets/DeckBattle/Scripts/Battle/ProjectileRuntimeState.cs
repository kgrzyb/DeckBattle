using System;

namespace DeckBattle
{
    public sealed class ProjectileRuntimeState
    {
        public readonly int ProjectileId;
        public readonly int AttackerUnitId;
        public readonly int TargetUnitId;
        public readonly UnitDefinition AttackerDefinition;
        public readonly ProjectileDefinition ProjectileDefinition;
        public readonly HexCoord FromHex;
        public HexCoord LastKnownTargetHex;
        public float TravelTimeRemaining;
        public readonly float TravelDuration;
        public readonly int Damage;
        public readonly bool IsCritical;

        public ProjectileRuntimeState(
            int projectileId,
            int attackerUnitId,
            int targetUnitId,
            UnitDefinition attackerDefinition,
            ProjectileDefinition projectileDefinition,
            HexCoord fromHex,
            HexCoord lastKnownTargetHex,
            float travelDuration,
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
            AttackerDefinition = attackerDefinition ?? throw new ArgumentNullException(nameof(attackerDefinition));
            ProjectileDefinition = projectileDefinition ?? throw new ArgumentNullException(nameof(projectileDefinition));
            FromHex = fromHex;
            LastKnownTargetHex = lastKnownTargetHex;
            TravelDuration = Math.Max(0f, travelDuration);
            TravelTimeRemaining = TravelDuration;
            Damage = Math.Max(0, damage);
            IsCritical = isCritical;
        }
    }
}
