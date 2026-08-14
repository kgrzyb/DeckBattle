using UnityEngine;

namespace DeckBattle
{
    public enum UnitSpecialKind
    {
        None = 0,
        HasteBurst = 1,
        FurySwipes = 2,
        Slam = 3,
        MegaArrow = 4,
        Longshot = 5
    }

    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "Deck Battle/Unit Definition")]
    public sealed class UnitDefinition : CardDefinition
    {
        public string UnitId
        {
            get { return CardId; }
            set { CardId = value; }
        }

        public override CardKind CardKind
        {
            get { return DeckBattle.CardKind.Unit; }
        }

        public UnitType UnitType;
        public int MaxHp = 1;
        public int Attack = 1;
        public int Power = 1;
        public int AttackRange = 1;
        public float CritChance = 0f;
        public float CritMultiplier = 2f;
        [Min(0.001f)] public float AttacksPerSecond = 1f;
        [Range(0f, 1f)] public float AttackWindupPercent = 0.25f;
        public int ManaThreshold = 100;
        [Tooltip("Mana gained per second passively, and as one pulse for each basic attack or received positive damage event.")]
        public int ManaPerSecond = 20;
        public UnitSpecialDefinition Special;
        public float Armor = 0f;
        public float ArmorPenetration = 0f;
        public ProjectileDefinition Projectile;
        public UnitView UnitPrefab;
        [Min(0.01f)] public float RunAnimationSpeedMultiplier = 1f;
        public BattleVfxProfile VfxProfile;
        public UnitOnPlayEffectDefinition OnPlayEffect;

        protected override void OnValidate()
        {
            base.OnValidate();
            SetCardKind(DeckBattle.CardKind.Unit);
            MaxHp = Mathf.Max(1, MaxHp);
            Attack = Mathf.Max(0, Attack);
            Power = Mathf.Max(0, Power);
            AttackRange = Mathf.Max(1, AttackRange);
            CritChance = Mathf.Clamp(CritChance, 0f, 100f);
            CritMultiplier = Mathf.Max(1f, CritMultiplier);
            AttacksPerSecond = Mathf.Max(0.001f, AttacksPerSecond);
            AttackWindupPercent = Mathf.Clamp01(AttackWindupPercent);
            ManaThreshold = Mathf.Max(0, ManaThreshold);
            ManaPerSecond = Mathf.Max(0, ManaPerSecond);
            Armor = Mathf.Clamp(Armor, 0f, 100f);
            ArmorPenetration = Mathf.Clamp(ArmorPenetration, 0f, 100f);
            if (RunAnimationSpeedMultiplier <= 0f
                || float.IsNaN(RunAnimationSpeedMultiplier)
                || float.IsInfinity(RunAnimationSpeedMultiplier))
            {
                RunAnimationSpeedMultiplier = 1f;
            }
        }
    }
}
