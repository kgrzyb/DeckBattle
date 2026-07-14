using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "Deck Battle/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        public string UnitId;
        public string DisplayName;
        public UnitType UnitType;
        public UnitRarity Rarity;
        public int ApCost = 1;
        public int MaxHp = 1;
        public int Attack = 1;
        public int Power = 1;
        public int AttackRange = 1;
        public int MoveRange = 1;
        public float AttackCooldown = 1f;
        public GameObject UnitPrefab;
        public Sprite CardArt;

        private void OnValidate()
        {
            ApCost = Mathf.Max(0, ApCost);
            MaxHp = Mathf.Max(1, MaxHp);
            Attack = Mathf.Max(0, Attack);
            Power = Mathf.Max(0, Power);
            AttackRange = Mathf.Max(1, AttackRange);
            MoveRange = Mathf.Max(0, MoveRange);
            AttackCooldown = Mathf.Max(0.1f, AttackCooldown);
        }
    }
}
