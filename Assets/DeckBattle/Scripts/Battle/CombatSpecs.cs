using System;

namespace DeckBattle
{
    public static class BattlePresentationId
    {
        public static int ForUnit(UnitDefinition definition)
        {
            string identifier = definition != null && !string.IsNullOrEmpty(definition.UnitId)
                ? definition.UnitId
                : definition != null ? definition.name : string.Empty;
            return FromIdentifier("unit", identifier);
        }

        public static int ForProjectile(ProjectileDefinition definition)
        {
            string identifier = definition != null && !string.IsNullOrEmpty(definition.ProjectileId)
                ? definition.ProjectileId
                : definition != null ? definition.name : string.Empty;
            return FromIdentifier("projectile", identifier);
        }

        private static int FromIdentifier(string category, string identifier)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < category.Length; i++)
                {
                    hash = (hash ^ category[i]) * 16777619u;
                }

                hash = (hash ^ ':') * 16777619u;
                for (int i = 0; i < identifier.Length; i++)
                {
                    hash = (hash ^ identifier[i]) * 16777619u;
                }

                int presentationId = (int)(hash & 0x7fffffffu);
                return presentationId != 0 ? presentationId : 1;
            }
        }
    }

    public readonly struct StatusCombatSpec
    {
        public readonly StatusKind Kind;
        public readonly StatusCategory Category;
        public readonly StatusStackingRule StackingRule;
        public readonly StatusValueCombinationRule IndependentPerSourceCombination;
        public readonly float DefaultDuration;
        public readonly float DefaultInterval;
        public readonly float DefaultMagnitude;
        public readonly int MaxStacks;

        public StatusCombatSpec(
            StatusKind kind,
            StatusCategory category,
            StatusStackingRule stackingRule,
            StatusValueCombinationRule independentPerSourceCombination,
            float defaultDuration,
            float defaultInterval,
            float defaultMagnitude,
            int maxStacks)
        {
            Kind = kind;
            Category = category;
            StackingRule = stackingRule;
            IndependentPerSourceCombination = independentPerSourceCombination;
            DefaultDuration = Math.Max(0.01f, defaultDuration);
            DefaultInterval = Math.Max(0f, defaultInterval);
            DefaultMagnitude = Math.Max(0f, defaultMagnitude);
            MaxStacks = Math.Max(1, maxStacks);
        }

        public static StatusCombatSpec FromDefinition(StatusDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new StatusCombatSpec(
                definition.Kind,
                definition.Category,
                definition.StackingRule,
                definition.IndependentPerSourceCombination,
                definition.DefaultDuration,
                definition.DefaultInterval,
                definition.DefaultMagnitude,
                definition.MaxStacks);
        }
    }

    public readonly struct UnitSpecialCombatSpec
    {
        public readonly UnitSpecialKind Kind;
        public readonly float WindupDuration;
        public readonly float CastDuration;
        public readonly StatusCombatSpec AppliedStatus;

        public bool IsValid
        {
            get { return Kind != UnitSpecialKind.None && AppliedStatus.Kind != StatusKind.None; }
        }

        public UnitSpecialCombatSpec(UnitSpecialKind kind, float windupDuration, float castDuration, StatusCombatSpec appliedStatus)
        {
            Kind = kind;
            WindupDuration = Math.Max(0f, windupDuration);
            CastDuration = Math.Max(0f, castDuration);
            AppliedStatus = appliedStatus;
        }

        public static UnitSpecialCombatSpec FromDefinition(UnitSpecialDefinition definition)
        {
            return definition == null || definition.AppliedStatus == null
                ? default
                : new UnitSpecialCombatSpec(
                    definition.Kind,
                    definition.WindupDuration,
                    definition.CastDuration,
                    StatusCombatSpec.FromDefinition(definition.AppliedStatus));
        }
    }

    public readonly struct ProjectileCombatSpec
    {
        public readonly int PresentationId;
        public readonly float Speed;

        public bool IsValid
        {
            get { return PresentationId != 0 && Speed > 0f; }
        }

        public ProjectileCombatSpec(int presentationId, float speed)
        {
            PresentationId = presentationId;
            Speed = Math.Max(0.01f, speed);
        }

        public static ProjectileCombatSpec FromDefinition(ProjectileDefinition definition)
        {
            return definition == null
                ? default
                : new ProjectileCombatSpec(BattlePresentationId.ForProjectile(definition), definition.Speed);
        }
    }

    public readonly struct UnitCombatSpec
    {
        public readonly int DefinitionId;
        public readonly int PresentationId;
        public readonly UnitType UnitType;
        public readonly int MaxHp;
        public readonly int Attack;
        public readonly int Power;
        public readonly int AttackRange;
        public readonly float CritChance;
        public readonly float CritMultiplier;
        public readonly float AttackCooldown;
        public readonly float AttackWindupPercent;
        public readonly int ManaThreshold;
        public readonly int ManaPerAttack;
        public readonly int ManaPerDamageTaken;
        public readonly float Armor;
        public readonly float ArmorPenetration;
        public readonly ProjectileCombatSpec Projectile;
        public readonly UnitSpecialCombatSpec Special;

        public bool HasProjectile
        {
            get { return Projectile.IsValid; }
        }

        public bool HasSpecial
        {
            get { return Special.IsValid; }
        }

        public UnitCombatSpec(
            int definitionId,
            int presentationId,
            UnitType unitType,
            int maxHp,
            int attack,
            int power,
            int attackRange,
            float critChance,
            float critMultiplier,
            float attackCooldown,
            float attackWindupPercent,
            int manaThreshold,
            int manaPerAttack,
            int manaPerDamageTaken,
            float armor,
            float armorPenetration,
            ProjectileCombatSpec projectile,
            UnitSpecialCombatSpec special)
        {
            DefinitionId = definitionId;
            PresentationId = presentationId;
            UnitType = unitType;
            MaxHp = Math.Max(1, maxHp);
            Attack = Math.Max(0, attack);
            Power = Math.Max(0, power);
            AttackRange = Math.Max(1, attackRange);
            CritChance = ClampPercentage(critChance);
            CritMultiplier = Math.Max(1f, critMultiplier);
            AttackCooldown = Math.Max(0.01f, attackCooldown);
            AttackWindupPercent = Math.Max(0f, Math.Min(1f, attackWindupPercent));
            ManaThreshold = Math.Max(0, manaThreshold);
            ManaPerAttack = Math.Max(0, manaPerAttack);
            ManaPerDamageTaken = Math.Max(0, manaPerDamageTaken);
            Armor = ClampPercentage(armor);
            ArmorPenetration = ClampPercentage(armorPenetration);
            Projectile = projectile;
            Special = special;
        }

        public static UnitCombatSpec FromDefinition(UnitDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int definitionId = BattlePresentationId.ForUnit(definition);
            return new UnitCombatSpec(
                definitionId,
                definitionId,
                definition.UnitType,
                definition.MaxHp,
                definition.Attack,
                definition.Power,
                definition.AttackRange,
                definition.CritChance,
                definition.CritMultiplier,
                1f / Math.Max(0.001f, definition.AttacksPerSecond),
                definition.AttackWindupPercent,
                definition.ManaThreshold,
                definition.ManaPerAttack,
                definition.ManaPerDamageTaken,
                definition.Armor,
                definition.ArmorPenetration,
                ProjectileCombatSpec.FromDefinition(definition.Projectile),
                UnitSpecialCombatSpec.FromDefinition(definition.Special));
        }

        private static float ClampPercentage(float value)
        {
            return Math.Max(0f, Math.Min(100f, value));
        }
    }
}
