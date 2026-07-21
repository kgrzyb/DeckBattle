using UnityEngine;

namespace DeckBattle
{
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
        public float AttackCooldown = 1f;
        public int ManaThreshold = 100;
        public int ManaPerAttack = 10;
        public int ManaPerDamageTaken = 10;
        public float Armor = 0f;
        public float ArmorPenetration = 0f;
        public ProjectileDefinition Projectile;
        public UnitView UnitPrefab;

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
            AttackCooldown = Mathf.Max(0.01f, AttackCooldown);
            ManaThreshold = Mathf.Max(0, ManaThreshold);
            ManaPerAttack = Mathf.Max(0, ManaPerAttack);
            ManaPerDamageTaken = Mathf.Max(0, ManaPerDamageTaken);
            Armor = Mathf.Clamp(Armor, 0f, 100f);
            ArmorPenetration = Mathf.Clamp(ArmorPenetration, 0f, 100f);
        }
    }
}
