using UnityEngine;
using UnityEngine.Serialization;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "UnitSpecialDefinition", menuName = "Deck Battle/Unit Special Definition")]
    public sealed class UnitSpecialDefinition : ScriptableObject
    {
        public const int MaxStrikeCount = 10;

        [InspectorName("Id")] public string SpecialId;
        public UnitSpecialKind Kind;
        [InspectorName("Description")]
        [TextArea] public string DescriptionTemplate;
        [FormerlySerializedAs("WindupDuration")]
        [Tooltip("Delay from cast start to the special payload. This does not delay mana spend or the cast animation.")]
        [Min(0f)] public float EffectDelay;
        [Min(0f)] public float CastDuration;
        public StatusDefinition AppliedStatus;
        public StatusLifetimeMode AppliedStatusLifetimeMode = StatusLifetimeMode.UseDefinitionDuration;
        [Min(0f)] public float AppliedStatusDurationOverride;
        public ProjectileDefinition Projectile;
        [Range(1, MaxStrikeCount)] public int StrikeCount = 1;
        [Min(0f)] public float AttackDamageMultiplier = 1f;
        [Min(0)] public int EffectRadius;
        [Range(0, 100)] public int ExecuteHpThresholdPercent;
        public BattleVfxProfile VfxProfile;

        private void OnValidate()
        {
            EffectDelay = Mathf.Max(0f, EffectDelay);
            CastDuration = Mathf.Max(0f, CastDuration);
            if (CastDuration > 0f)
            {
                EffectDelay = Mathf.Min(EffectDelay, CastDuration);
            }
            AppliedStatusDurationOverride = Mathf.Max(0f, AppliedStatusDurationOverride);
            StrikeCount = Mathf.Clamp(StrikeCount, 1, MaxStrikeCount);
            AttackDamageMultiplier = Mathf.Max(0f, AttackDamageMultiplier);
            EffectRadius = Mathf.Max(0, EffectRadius);
            ExecuteHpThresholdPercent = Mathf.Clamp(ExecuteHpThresholdPercent, 0, 100);
        }
    }
}
