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
        public readonly float EffectDelay;
        public readonly float CastDuration;
        public readonly StatusCombatSpec AppliedStatus;
        public readonly StatusLifetimeMode AppliedStatusLifetimeMode;
        public readonly float AppliedStatusDuration;
        public readonly ProjectileCombatSpec Projectile;
        public readonly int StrikeCount;
        public readonly float AttackDamageMultiplier;
        public readonly int EffectRadius;
        public readonly int ExecuteHpThresholdPercent;

        public bool IsValid
        {
            get
            {
                switch (Kind)
                {
                    case UnitSpecialKind.HasteBurst:
                        return AppliedStatus.Kind == StatusKind.Haste;
                    case UnitSpecialKind.FurySwipes:
                        return CastDuration > 0f
                            && StrikeCount > 0
                            && AttackDamageMultiplier > 0f;
                    case UnitSpecialKind.Slam:
                        return AttackDamageMultiplier > 0f
                            && EffectRadius >= 0;
                    case UnitSpecialKind.MegaArrow:
                        return CastDuration > 0f
                            && EffectDelay <= CastDuration
                            && Projectile.IsValid
                            && AppliedStatus.Kind == StatusKind.Stun
                            && AttackDamageMultiplier > 0f
                            && (AppliedStatusLifetimeMode != StatusLifetimeMode.OverrideSeconds
                                || AppliedStatusDuration > 0f);
                    case UnitSpecialKind.Longshot:
                        return CastDuration > 0f
                            && EffectDelay <= CastDuration
                            && Projectile.IsValid
                            && AttackDamageMultiplier > 0f
                            && ExecuteHpThresholdPercent > 0
                            && ExecuteHpThresholdPercent < 100;
                    default:
                        return false;
                }
            }
        }

        public UnitSpecialCombatSpec(
            UnitSpecialKind kind,
            float effectDelay,
            float castDuration,
            StatusCombatSpec appliedStatus,
            StatusLifetimeMode appliedStatusLifetimeMode,
            float appliedStatusDuration,
            ProjectileCombatSpec projectile,
            int strikeCount,
            float attackDamageMultiplier,
            int effectRadius,
            int executeHpThresholdPercent)
        {
            Kind = kind;
            CastDuration = Math.Max(0f, castDuration);
            EffectDelay = Math.Min(Math.Max(0f, effectDelay), CastDuration);
            AppliedStatus = appliedStatus;
            AppliedStatusLifetimeMode = appliedStatusLifetimeMode;
            AppliedStatusDuration = appliedStatusDuration;
            Projectile = projectile;
            StrikeCount = Math.Max(1, Math.Min(UnitSpecialDefinition.MaxStrikeCount, strikeCount));
            AttackDamageMultiplier = Math.Max(0f, attackDamageMultiplier);
            EffectRadius = Math.Max(0, effectRadius);
            ExecuteHpThresholdPercent = Math.Max(0, Math.Min(100, executeHpThresholdPercent));
        }

        public static UnitSpecialCombatSpec FromDefinition(UnitSpecialDefinition definition)
        {
            if (definition == null)
            {
                return default;
            }

            StatusCombatSpec appliedStatus = definition.AppliedStatus != null
                ? StatusCombatSpec.FromDefinition(definition.AppliedStatus)
                : default;
            float appliedStatusDuration = definition.AppliedStatusLifetimeMode == StatusLifetimeMode.OverrideSeconds
                ? definition.AppliedStatusDurationOverride
                : -1f;
            return new UnitSpecialCombatSpec(
                definition.Kind,
                definition.EffectDelay,
                definition.CastDuration,
                appliedStatus,
                definition.AppliedStatusLifetimeMode,
                appliedStatusDuration,
                ProjectileCombatSpec.FromDefinition(definition.Projectile),
                definition.StrikeCount,
                definition.AttackDamageMultiplier,
                definition.EffectRadius,
                definition.ExecuteHpThresholdPercent);
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
        public readonly int ManaPerSecond;
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
            int manaPerSecond,
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
            ManaPerSecond = Math.Max(0, manaPerSecond);
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
                definition.ManaPerSecond,
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
